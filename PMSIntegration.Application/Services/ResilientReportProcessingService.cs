using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Enums;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Application.Services;

/// <summary>
/// Orchestrates report processing with resilience patterns
/// Handles the full lifecycle: UPLOADED -> PROCESSED -> IMPORTED -> SUCCESS
/// </summary>
public class ResilientReportProcessingService
{
    private const int MAX_RETRY_ATTEMPTS = 3;
    
    private readonly IReportRepository _reportRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IPmsFileSystemService _pmsFileSystem;
    private readonly IResiliencePolicy _resiliencePolicy;
    private readonly ILogger<ResilientReportProcessingService> _logger;
    
    public ResilientReportProcessingService(
        IReportRepository reportRepository,
        IPatientRepository patientRepository,
        IPmsFileSystemService pmsFileSystem,
        IResiliencePolicy resiliencePolicy,
        ILogger<ResilientReportProcessingService> logger)
    {
        _reportRepository = reportRepository;
        _patientRepository = patientRepository;
        _pmsFileSystem = pmsFileSystem;
        _resiliencePolicy = resiliencePolicy;
        _logger = logger;
    }

    /// <summary>
    /// Process a newly added file to reports folder
    /// </summary>
    /// <returns></returns>
    public async Task<ReportProcessingResult> ProcessNewReportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation($"Starting processing for new report: {fileName}");

        try
        {
            var absolutePath = Path.IsPathRooted(filePath) 
                ? filePath 
                : Path.GetFullPath(filePath);
            // Step1: create report record in db
            var report = Report.CreateUploaded(fileName, absolutePath);
            report.Id = await _reportRepository.InsertAsync(report);

            _logger.LogInformation($"Report registered with ID: {report.Id}");

            // Step2: Process the report ( identify patient )
            await ProcessReportAsync(report, cancellationToken);

            return new ReportProcessingResult
            {
                Success = true,
                ReportId = report.Id,
                Message = $"Report processed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process new report:{fileName}");
            return new ReportProcessingResult
            {
                Success = false,
                Message = $"Error processing report: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Process pending reports ( retry failed ones )
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<int> ProcessPendingReportsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing pending reports");

        var uploadedReports = await _reportRepository.GetByStatusAsync(ReportStatus.UPLOADED);
        var processedReports = await _reportRepository.GetByStatusAsync(ReportStatus.PROCESSED);
        var importedReports = await _reportRepository.GetByStatusAsync(ReportStatus.IMPORTED);
        
        var allPending = uploadedReports
            .Concat(processedReports)
            .Concat(importedReports)
            .ToList();

        if (!allPending.Any())
        {
            _logger.LogInformation("No pending reports to process");
            return 0;
        }
        
        _logger.LogInformation($"Found {allPending.Count} pending reports");

        var processedCount = 0;

        foreach (var report in allPending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Processing cancelled by shutdown request");
                break;
            }

            try
            {
                await ProcessReportAsync(report, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing report: {report.Id}: {report.FileName}");
            }
            
            // Small delay between reports to avoid overwhelming the system
            await Task.Delay(500, cancellationToken);
        }
        
        return processedCount;
    }

    /// <summary>
    /// Main processing logic - state machine
    /// </summary>
    /// <param name="report"></param>
    /// <param name="cancellationToken"></param>
    private async Task ProcessReportAsync(Report report, CancellationToken cancellationToken)
    {
        try
        {
            switch (report.Status)
            {
                case ReportStatus.UPLOADED:
                    await TransitionToProcessed(report);
                    break;
                case ReportStatus.PROCESSED:
                    await TransitionToImported(report);
                    break;
                case ReportStatus.IMPORTED:
                    await TransitionToSuccess(report);
                    break;

                case ReportStatus.SUCCESS:
                    _logger.LogDebug($"Report {report.Id} already completed");
                    break;
                case ReportStatus.FAILED:
                    _logger.LogWarning($"Report {report.Id} is in FAILED state, skipping");
                    break;
            }
        }
        catch (Exception ex)
        {
            await HandleReportError(report, ex);
        }
    }

    /// <summary>
    /// UPLOADED -> PROCESSED: Identify patient name from filename
    /// </summary>
    /// <param name="report"></param>
    private async Task TransitionToProcessed(Report report)
    {
        _logger.LogInformation($"Processing report {report.Id}: Identifying patient");
        
        // Extract patient name from filename
        var patientName = ExtractPatientNameFromFileName(report.FileName);

        if (string.IsNullOrWhiteSpace(patientName))
        {
            _logger.LogError($"Could not extract patient name from filename: {report.FileName}");
        }
        
        var patient = await FindPatientByName(patientName);

        if (patient == null)
        {
            _logger.LogWarning($"Patient not found: {patientName}");
        }
        
        // Update report status
        report.Status = ReportStatus.PROCESSED;
        report.PatientId = patient.Id;
        report.PatientName = $"{patient.FirstName} {patient.LastName}";
        report.ProcessedAt = DateTime.UtcNow;
        
        await _reportRepository.UpdateAsync(report);
        
        _logger.LogInformation($"Report {report.Id} processed: Patient {report.PatientName} (ID: {report.PatientId})");
        
        // Continue to next step
        await TransitionToImported(report);
    }

    /// <summary>
    /// PROCESSED -> IMPORTED: Move file to patient folder or Reports callback
    /// </summary>
    /// <param name="report"></param>
    private async Task TransitionToImported(Report report)
    {
        if (report.PatientId == null || string.IsNullOrWhiteSpace(report.PatientName))
        {
            _logger.LogError($"Report {report.Id} missing patient information");
        }

        _logger.LogInformation($"Importing report {report.Id}: Moving file to patient folder");

        // Use resilience policy for file operations
        var destinationPath = await _resiliencePolicy.ExecuteAsync(async () =>
        {
            // Try to find patient folder
            var patientFolder = await _pmsFileSystem.FindPatientFolderAsync(
                report.PatientName,
                report.PatientId.Value);

            string targetFolder;

            if (patientFolder != null)
            {
                targetFolder = patientFolder;
                _logger.LogInformation($"Using patient folder: {targetFolder}");
            }
            else
            {
                // Fallback to Reports folder
                targetFolder = _pmsFileSystem.GetReportsFallbackPath();
                _logger.LogWarning($"Patient folder not found, using fallback: {targetFolder}");
            }

            // Move file
            return await _pmsFileSystem.MoveReportToPatientFolderAsync(
                report.SourcePath,
                targetFolder,
                report.FileName);
        });
        
        // Update report status
        report.Status = ReportStatus.IMPORTED;
        report.DestinationPath = destinationPath;
        report.ImportedAt = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(report);
        
        _logger.LogInformation($"Report {report.Id} imported to: {destinationPath}");

        // Mark patient as having report ready
        var patient = await _patientRepository.GetByIdAsync(report.PatientId.Value);
        if (patient != null)
        {
            patient.ReportReady = true;
            await _patientRepository.UpdateAsync(patient);
        }

        // Continue to next step
        await TransitionToSuccess(report);
    }
    
    /// <summary>
    /// IMPORTED -> SUCCESS: Delete source file
    /// </summary>
    private async Task TransitionToSuccess(Report report)
    {
        _logger.LogInformation($"Completing report {report.Id}: Cleaning up source file");

        // Delete source file with resilience
        await _resiliencePolicy.ExecuteAsync(async () =>
        {
            if (File.Exists(report.SourcePath))
            {
                await Task.Run(() => File.Delete(report.SourcePath));
                _logger.LogInformation($"Deleted source file: {report.SourcePath}");
            }
            else
            {
                _logger.LogWarning($"Source file already deleted: {report.SourcePath}");
            }
        });

        // Update report status
        report.Status = ReportStatus.SUCCESS;
        report.CompletedAt = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(report);
        
        var processingTime = report.CompletedAt.Value - report.CreatedAt;
        _logger.LogInformation($"Report {report.Id} completed successfully in {processingTime.TotalSeconds:F2}s");
        
    }
    
    /// <summary>
    /// Handle errors and update report status
    /// </summary>
    private async Task HandleReportError(Report report, Exception exception)
    {
        _logger.LogError(exception, $"Error processing report {report.Id}: {report.FileName}");

        report.Status = ReportStatus.FAILED;
        report.ErrorMessage = exception.Message;

        await _reportRepository.UpdateAsync(report);
    }

    /// <summary>
    /// Extract patient name from filename
    /// Expected format: "LastName FirstName.pdf" (e.g., "Sutherlun Jonah.pdf")
    /// </summary>
    private string ExtractPatientNameFromFileName(string fileName)
    {
        // Remove extension: "Sutherlun Jonah.pdf" -> "Sutherlun Jonah"
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        
        if (string.IsNullOrWhiteSpace(nameWithoutExt))
            return string.Empty;
        
        return nameWithoutExt.Trim();
    }

    /// <summary>
    /// Find patient by name (fuzzy match)
    /// Expected input: "Sutherlun Jonah" (LastName FirstName)
    /// </summary>
    private async Task<Patient?> FindPatientByName(string patientName)
    {
        var allPatients = await _patientRepository.GetAllAsync();
        
        // Normalize search string
        var normalizedSearch = patientName.Replace(" ", "").ToLower();
        
        return allPatients.FirstOrDefault(p => 
        {
            // Try matching: LastName + FirstName
            var lastFirst = $"{p.LastName}{p.FirstName}".ToLower();
            
            // Try matching: FirstName + LastName
            var firstLast = $"{p.FirstName}{p.LastName}".ToLower();
            
            return lastFirst == normalizedSearch || firstLast == normalizedSearch;
        });
    }
}