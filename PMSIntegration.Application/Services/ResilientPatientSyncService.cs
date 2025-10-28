using Microsoft.Extensions.Logging;
using PMSIntegration.Application.Exceptions;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Enums;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Application.Services;

/// <summary>
/// Orchestrates patient synchronization with resilience patterns
/// </summary>
public class ResilientPatientSyncService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IInsuranceRepository _insuranceRepository;
    private readonly IPmsApiService _pmsApiService;
    private readonly IResiliencePolicy _resiliencePolicy;
    private readonly ILogger<ResilientPatientSyncService> _logger;
    private readonly ISyncConfiguration _configuration;

    public ResilientPatientSyncService(
        IPatientRepository patientRepository,
        IInsuranceRepository insuranceRepository,
        IPmsApiService pmsApiService,
        IResiliencePolicy resiliencePolicy,
        ILogger<ResilientPatientSyncService> logger,
        ISyncConfiguration configuration)
    {
        _patientRepository = patientRepository;
        _insuranceRepository = insuranceRepository;
        _pmsApiService = pmsApiService;
        _resiliencePolicy = resiliencePolicy;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
    {
        var syncState = await _patientRepository.GetSyncStateAsync();
        if (syncState == null)
        {
            syncState = new SyncState
            {
                Status = SyncStatus.Pending,
                FailedAttempts = 0
            };
            
            await _patientRepository.UpdateSyncStateAsync(syncState);
        }
        
        syncState.LastAttemptedSync = DateTime.UtcNow;
        syncState.Status = SyncStatus.InProgress;

        try
        {
            // Check API availability with resilience
            var isAvailable = await _resiliencePolicy.ExecuteAsync(
                async () => await _pmsApiService.IsAvailableAsync());

            if (!isAvailable)
            {
                _logger.LogWarning("PMS API is not available, will retry later");
                syncState.Status = SyncStatus.Retrying;
                syncState.FailedAttempts++;
                await _patientRepository.UpdateSyncStateAsync(syncState);
                return new SyncResult { Success = false, Message = "API unavailable" };
            }

            // Determine the date to sync from
            DateTime syncFromDate;
            if (syncState.LastSuccessfulSync.HasValue)
            {
                // If we've synced before, sync from the last successful sync
                syncFromDate = syncState.LastSuccessfulSync.Value;
                _logger.LogInformation($"Syncing patients modified since last sync: {syncFromDate:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                // First sync - use configured start date
                syncFromDate = _configuration.ExportStartDate;
                _logger.LogInformation($"First sync - fetching patients from configured start date: {syncFromDate:yyyy-MM-dd}");
            }

            // Get patients since the determined date
            var patients = await _resiliencePolicy.ExecuteAsync(
                async () => await _pmsApiService.GetPatientsAsync(syncFromDate)
            );

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Sync cancelled by shutdown request");
                syncState.Status = SyncStatus.Pending;
                await _patientRepository.UpdateSyncStateAsync(syncState);
                return new SyncResult { Success = false, Message = "Cancelled by shutdown request" };
            }

            // Filter new patients
            var existingIds = await _patientRepository.GetAllIdsAsync();
            var newPatients = patients.Where(p => !existingIds.Contains(p.Id)).ToList();

            if (newPatients.Any())
            {
                // Save with transactions
                var savedPatients = await _patientRepository.BulkSaveAsync(newPatients);
                _logger.LogInformation($"Saved {savedPatients} new patients");

                //Process insurance data in background
                var savedInsurance = await ProcessInsuranceAsync(newPatients.Select(p => p.Id).ToList());
                _logger.LogInformation($"Saved {savedInsurance} insurances");
            }
            else
            {
                _logger.LogInformation("No new patients to sync");
            }

            syncState.LastSuccessfulSync = DateTime.UtcNow;
            syncState.Status = SyncStatus.Completed;
            syncState.FailedAttempts = 0;
            syncState.LastError = null;
            await _patientRepository.UpdateSyncStateAsync(syncState);

            return new SyncResult
            {
                Success = true,
                ProcessedCount = newPatients.Count,
                Message = $"Successfully synced {newPatients.Count} patients"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during patient sync");
            
            syncState.Status = SyncStatus.Failed;
            syncState.FailedAttempts++;
            syncState.LastError = ex.Message;
            await _patientRepository.UpdateSyncStateAsync(syncState);

            if (!syncState.ShouldRetry)
            {
                throw new MaxRetriesExceededException(
                    "Patient sync failed after maximum retry attempts", ex);
            }

            return new SyncResult
            {
                Success = false,
                Message = $"Sync failed: {ex.Message}"
            };
        }
    }

    private async Task<int> ProcessInsuranceAsync(List<int> patientIds)
    {
        // Process in batches to avoid overwhelming the API
        const int batchSize = 10;
        var saved = 0;

        for (int i = 0; i < patientIds.Count; i+= batchSize)
        {
            var batch = patientIds.Skip(i).Take(batchSize);

            foreach (var patientId in batch)
            {
                try
                {
                    await _resiliencePolicy.ExecuteAsync(async () =>
                    {
                        var insurance = await _pmsApiService.GetPatientInsuranceAsync(patientId);
                        saved += await _insuranceRepository.BulkSaveAsync(insurance);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to sync insurance for patient {patientId}");
                }
            }

            // Rate limiting
            await Task.Delay(500);
        }

        return saved;
    }
}