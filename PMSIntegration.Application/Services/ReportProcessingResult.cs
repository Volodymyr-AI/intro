namespace PMSIntegration.Application.Services;

public class ReportProcessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ReportId { get; set; }
}