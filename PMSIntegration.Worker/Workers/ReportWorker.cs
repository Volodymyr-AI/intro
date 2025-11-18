using System.Globalization;
using System.Threading.Channels;
using PMSIntegration.Application.Services;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Worker.Workers;

public class ReportWorker : BackgroundService
{
    private const int MAX_CONSECUTIVE_FAILURES = 10;
    private const int PROCESSING_INTERVAL_MINUTES = 5;
    private const int QUEUE_CAPACITY = 100; // Maximum queue size of processed studies
    
    private readonly ILogger<ReportWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly string _reportsDirectory;
    
    private FileSystemWatcher? _fileWatcher;
    private int _consecutiveFailures = 0;
    
    // Channel-based queue for controlled processing
    private readonly Channel<string> _reportQueue;
    private int _queuedCount = 0;
    private int _processedCount = 0;

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
        
        _reportQueue = Channel.CreateBounded<string>(
            new BoundedChannelOptions(QUEUE_CAPACITY)
            {
                FullMode = BoundedChannelFullMode.Wait // Block if queue is full
            });
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
        
        // Queue existing files
        await QueueExistingFilesAsync();
        
        // Start file system watcher
        StartFileSystemWatcher();
        
        // Start background task for periodic pending reports check
        _ = Task.Run(() => ProcessPendingReportsLoop(stoppingToken), stoppingToken);
        
        // Main queue processing loop
        await ProcessQueueLoop(stoppingToken);
        
        _logger.LogInformation("ReportWorker stopped");
    }
    
    /// <summary>
    /// Main queue processing loop - processes reports one by one
    /// </summary>
    private async Task ProcessQueueLoop(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting queue processing loop");
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait for next report in queue
                if (await _reportQueue.Reader.WaitToReadAsync(stoppingToken))
                {
                    if (_reportQueue.Reader.TryRead(out var filePath))
                    {
                        var queueSize = _queuedCount - _processedCount;
                        _logger.LogDebug($"Processing report from queue ({queueSize} remaining): {Path.GetFileName(filePath)}");
                        
                        try
                        {
                            await ProcessReportFileAsync(filePath);
                            _processedCount++;
                            _consecutiveFailures = 0;
                        }
                        catch (Exception ex)
                        {
                            _consecutiveFailures++;
                            _logger.LogError(ex, $"Error processing report: {filePath}");
                            
                            if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                            {
                                _logger.LogCritical($"Reached max consecutive failures ({MAX_CONSECUTIVE_FAILURES}). Stopping service.");
                                _lifetime.StopApplication();
                                return;
                            }
                            
                            // Small delay before processing next file after error
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Queue processing cancelled due to shutdown");
        }
    }

    /// <summary>
    /// Periodic loop for processing pending reports (retry logic)
    /// </summary>
    private async Task ProcessPendingReportsLoop(CancellationToken stoppingToken)
    {
        // Wait for initial startup
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingReportsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(PROCESSING_INTERVAL_MINUTES), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pending reports processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pending reports loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Queue existing files on startup
    /// </summary>
    private async Task QueueExistingFilesAsync()
    {
        try
        {
            var files = Directory.GetFiles(_reportsDirectory, "*.pdf");
            
            if (files.Length == 0)
            {
                _logger.LogInformation("No existing report files found");
                return;
            }

            _logger.LogInformation($"Queueing {files.Length} existing report files");

            foreach (var file in files)
            {
                await EnqueueReportAsync(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing existing files");
        }
    }

    /// <summary>
    /// Enqueue a report file for processing
    /// </summary>
    private async Task EnqueueReportAsync(string filePath)
    {
        try
        {
            // Wait for file to be fully written
            if (!await WaitForFileAccessAsync(filePath))
            {
                _logger.LogWarning($"Could not access file, skipping: {filePath}");
                return;
            }
            
            await _reportQueue.Writer.WriteAsync(filePath);
            _queuedCount++;
            
            var queueSize = _queuedCount - _processedCount;
            _logger.LogDebug($"Queued report: {Path.GetFileName(filePath)} (queue size: {queueSize})");
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Queue is closed, cannot enqueue report");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error enqueueing report: {filePath}");
        }
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
    /// Handle new file created event - just enqueue it
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation($"New report file detected: {e.Name}");

        // Enqueue in background to avoid blocking file watcher
        _ = Task.Run(async () =>
        {
            // Small delay to ensure file is fully written
            await Task.Delay(1000);
            await EnqueueReportAsync(e.FullPath);
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
    /// Process a single report file
    /// </summary>
    private async Task ProcessReportFileAsync(string filePath)
    {
        using var scope = _scopeFactory.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<ResilientReportProcessingService>();

        _logger.LogInformation($"Processing report: {Path.GetFileName(filePath)}");
        
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

    /// <summary>
    /// Wait for file to be accessible (not locked by another process)
    /// </summary>
    private async Task<bool> WaitForFileAccessAsync(string filePath, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning($"File disappeared: {filePath}");
                    return false;
                }
                
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

            _logger.LogDebug("Checking for pending reports...");
            
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
        
        // Close queue writer to signal completion
        _reportQueue.Writer.Complete();
        
        // Log queue statistics
        var remaining = _queuedCount - _processedCount;
        _logger.LogInformation($"Queue statistics - Queued: {_queuedCount}, Processed: {_processedCount}, Remaining: {remaining}");

        await base.StopAsync(cancellationToken);
    }
}