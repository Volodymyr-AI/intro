using PMSIntegration.Application.Services;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Worker.Workers;

public class ReportWorker : BackgroundService
{
    private const int MAX_CONSECUTIVE_FAILURES = 10;
    private const int PROCESSING_INTERVAL_MINUTES = 5;
    
    private readonly ILogger<ReportWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly string _reportsDirectory;
    
    private FileSystemWatcher? _fileWatcher;
    private int _consecutiveFailures = 0;
    private readonly object _lockObject = new object();
    private readonly HashSet<string> _processingFiles = new HashSet<string>();

    public ReportWorker(
        ILogger<ReportWorker> logger, 
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _reportsDirectory = configuration["ReportsDirectory"] ?? "reports";
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportWorker starting");
        
        //Validate reports directory
        if (!Directory.Exists(_reportsDirectory))
        {
            _logger.LogWarning($"Reports directory not found, creating: {_reportsDirectory}");
            Directory.CreateDirectory(_reportsDirectory);
        }
        
        // Validate PMS file system access
        await ValidatePmsFileSystemAsync();
        
        // Initial delay to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        
        // Process any existing files first
        await ProcessExistingFilesAsync(stoppingToken);
        
        // Start file system watcher
        StartFileSystemWatcher();
        
        // Periodic processing loop for pending reports and retry logic
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingReportsAsync(stoppingToken);

                _consecutiveFailures = 0;

                // Wait before next cycle
                _logger.LogDebug($"Next pending reports check in {PROCESSING_INTERVAL_MINUTES} minutes");
                await Task.Delay(TimeSpan.FromMinutes(PROCESSING_INTERVAL_MINUTES), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Report processing cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, $"Error in report processing cycle (failures: {_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES})");

                if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                {
                    _logger.LogCritical($"Service reached max failures ({MAX_CONSECUTIVE_FAILURES}). Stopping.");
                    _lifetime.StopApplication();
                    return;
                }

                var delay = TimeSpan.FromMinutes(Math.Min(5 * _consecutiveFailures, 30));
                _logger.LogWarning($"Waiting {delay.TotalMinutes} minutes before retry");
                await Task.Delay(delay, stoppingToken);
            }
        }
        
        _logger.LogInformation("ReportWorker stopped");
    }
    
    /// <summary>
    /// Validate PMS file system is accessible
    /// </summary>
    private async Task ValidatePmsFileSystemAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pmsFileSystem = scope.ServiceProvider.GetRequiredService<IPmsFileSystemService>();
            
            var isValid = await pmsFileSystem.ValidateImageFolderAccessAsync();
            
            if (!isValid)
            {
                _logger.LogWarning("PMS Image folder validation failed. Reports may not be processed correctly.");
            }
            else
            {
                _logger.LogInformation("PMS Image folder validation successful");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PMS file system");
        }
    }

    /// <summary>
    /// Process any existing files in reports directory on startup
    /// </summary>
    private async Task ProcessExistingFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var files = Directory.GetFiles(_reportsDirectory, "*.pdf");
            
            if (files.Length == 0)
            {
                _logger.LogInformation("No existing report files found");
                return;
            }

            _logger.LogInformation($"Found {files.Length} existing report files to process");

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessNewFileAsync(file);
                
                // Small delay between files
                await Task.Delay(500, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing existing files");
        }
    }

    /// <summary>
    /// Start FileSystemWatcher to monitor new files
    /// </summary>
    private void StartFileSystemWatcher()
    {
        try
        {
            _fileWatcher = new FileSystemWatcher(_reportsDirectory)
            {
                Filter = "*.pdf",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Error += OnWatcherError;

            _logger.LogInformation($"FileSystemWatcher started monitoring: {_reportsDirectory}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FileSystemWatcher");
        }
    }

    /// <summary>
    /// Handle new file created event
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation($"New report file detected: {e.Name}");

        // Run processing in background task to avoid blocking file watcher
        _ = Task.Run(async () =>
        {
            // Wait a bit to ensure file is fully written
            await Task.Delay(1000);
            await ProcessNewFileAsync(e.FullPath);
        });
    }

    /// <summary>
    /// Handle file watcher errors
    /// </summary>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex, "FileSystemWatcher error occurred");

        // Try to restart watcher
        try
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.EnableRaisingEvents = true;
                _logger.LogInformation("FileSystemWatcher restarted after error");
            }
        }
        catch (Exception restartEx)
        {
            _logger.LogError(restartEx, "Failed to restart FileSystemWatcher");
        }
    }

    /// <summary>
    /// Process a new report file
    /// </summary>
    private async Task ProcessNewFileAsync(string filePath)
    {
        // Prevent duplicate processing
        lock (_lockObject)
        {
            if (_processingFiles.Contains(filePath))
            {
                _logger.LogDebug($"File already being processed: {filePath}");
                return;
            }
            _processingFiles.Add(filePath);
        }

        try
        {
            // Wait for file to be fully written and not locked
            if (!await WaitForFileAccessAsync(filePath))
            {
                _logger.LogWarning($"Could not access file after waiting: {filePath}");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<ResilientReportProcessingService>();

            _logger.LogInformation($"Processing new report: {Path.GetFileName(filePath)}");
            
            var result = await processingService.ProcessNewReportAsync(filePath);

            if (result.Success)
            {
                _logger.LogInformation($"Successfully processed report: {result.Message}");
            }
            else
            {
                _logger.LogWarning($"Failed to process report: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing file: {filePath}");
        }
        finally
        {
            lock (_lockObject)
            {
                _processingFiles.Remove(filePath);
            }
        }
    }

    /// <summary>
    /// Wait for file to be accessible (not locked by another process)
    /// </summary>
    private async Task<bool> WaitForFileAccessAsync(string filePath, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                // Try to open file exclusively
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                // File is locked, wait and retry
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking file access: {filePath}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Process pending reports (retry failed ones)
    /// </summary>
    private async Task ProcessPendingReportsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<ResilientReportProcessingService>();

            var processedCount = await processingService.ProcessPendingReportsAsync(cancellationToken);

            if (processedCount > 0)
            {
                _logger.LogInformation($"Processed {processedCount} pending reports");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending reports");
        }
    }

    /// <summary>
    /// Cleanup on service stop
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ReportWorker is stopping");

        // Stop file watcher
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= OnFileCreated;
            _fileWatcher.Error -= OnWatcherError;
            _fileWatcher.Dispose();
            _logger.LogInformation("FileSystemWatcher stopped");
        }

        await base.StopAsync(cancellationToken);
    }
}