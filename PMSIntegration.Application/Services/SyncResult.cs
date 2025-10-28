namespace PMSIntegration.Application.Services;

public class SyncResult
{
    public bool Success { get; set; }
    
    public int ProcessedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}