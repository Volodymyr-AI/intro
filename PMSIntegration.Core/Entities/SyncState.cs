using PMSIntegration.Core.Enums;

namespace PMSIntegration.Core.Entities;

/// <summary>
/// Tracks synchronization state for resilience
/// </summary>
public class SyncState
{
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastAttemptedSync { get; set; }
    public string? LastError { get; set; }
    public int FailedAttempts { get; set; }
    
    public SyncStatus Status { get; set; }

    public bool ShouldRetry =>
        Status != SyncStatus.Completed &&
        FailedAttempts <= 3;

    public TimeSpan GetBackoffDelay()
    {
        // Exponential backoff: 1min, 5min, 15min
        return FailedAttempts switch
        {
            0 => TimeSpan.FromMinutes(1),
            1 => TimeSpan.FromMinutes(5),
            2 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
    }
}