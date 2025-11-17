using PMSIntegration.Core.Enums;

namespace PMSIntegration.Core.Entities;

public class Report
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public string? PatientName { get;  set; }
    public string SourcePath { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public ReportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ImportedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static Report CreateUploaded(string fileName, string sourcePath)
    {
        return new Report
        {
            FileName = fileName,
            SourcePath = sourcePath,
            Status = ReportStatus.UPLOADED,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool HasValue<T, TValue>(T obj, Func<T, TValue> selector)
    {
        var value = selector(obj);
        return value != null && !Equals(value, default(TValue));
    }
}