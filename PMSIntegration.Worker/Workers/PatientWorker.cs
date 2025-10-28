using PMSIntegration.Application.Services;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Enums;

namespace PMSIntegration.Worker.Workers
{
    /// <summary>
    /// Background service that synchronizes patients from PMS
    /// </summary>
    public class PatientWorker : BackgroundService
    {
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 10;
        
        private readonly ILogger<PatientWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _lifetime;
        private SyncState? _currentSyncState;
        private Timer? _syncTimer;
        public PatientWorker(
            ILogger<PatientWorker> logger,
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime lifetime)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay to let the application fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSyncCycle(stoppingToken);

                    // Calculate next sync delay based on sync state
                    var delay = GetNextSyncDelay();
                    _logger.LogInformation($"Next sync scheduled in {delay.TotalMinutes:F1} minutes");

                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException ex)
                {
                    // Expected when cancellation is requested
                    _logger.LogInformation("Sync operation cancelled due to shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    {
                        _logger.LogCritical($"Service reached max failures ({MAX_CONSECUTIVE_FAILURES}). Stopping.");
                        _lifetime.StopApplication();
                        return;
                    }
    
                    var delay = TimeSpan.FromMinutes(Math.Min(5 * _consecutiveFailures, 30));
                    await Task.Delay(delay, stoppingToken);
                }
                _consecutiveFailures = 0;
            }
            
            _logger.LogInformation("Patient synchronization worker stopped");
        }

        private TimeSpan GetNextSyncDelay()
        {
            // If no sync state, use default interval
            if (_currentSyncState == null)
            {
                return TimeSpan.FromMinutes(15);
            }
            
            // If last sync was successful, use normal interval
            if (_currentSyncState.Status == SyncStatus.Completed)
            {
                return TimeSpan.FromMinutes(30);
            }
            
            // Use exponential backoff for failed syncs
            return _currentSyncState.GetBackoffDelay();
        }

        private async Task PerformSyncCycle(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting patient synchronization cycle");

                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ResilientPatientSyncService>();
                
                var result = await syncService.SyncDataAsync(cancellationToken);
                
                if (result.Success)
                {
                    _logger.LogInformation($"Sync completed successfully: {result.Message}");

                    _currentSyncState = new SyncState
                    {
                        LastSuccessfulSync = DateTime.UtcNow,
                        Status = Core.Enums.SyncStatus.Completed,
                        FailedAttempts = 0
                    };
                }
                else
                {
                    _logger.LogWarning($"Sync completed with issues: {result.Message}");

                    if (_currentSyncState == null)
                    {
                        _currentSyncState = new SyncState();
                    }

                    _currentSyncState.FailedAttempts++;
                    _currentSyncState.LastError = result.Message;
                    _currentSyncState.Status = Core.Enums.SyncStatus.Retrying;
                }
            }
            catch (Application.Exceptions.MaxRetriesExceededException ex)
            {
                _logger.LogError(ex, "Maximum retry attempts exceeded for patient sync");

                if (_currentSyncState == null)
                {
                    _currentSyncState = new SyncState();
                }
                
                _currentSyncState.Status = Core.Enums.SyncStatus.Failed;
                _currentSyncState.LastError = ex.Message;

                if (_currentSyncState.FailedAttempts > 10)
                {
                    _logger.LogCritical("Stopping service");
                    _lifetime.StopApplication();
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Patient synchronization worker is stopping");
            
            _syncTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }
}
