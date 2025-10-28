using PMSIntegration.Core.Reports.Enums;

namespace PMSIntegration.Core.Entities;

public class Report
{
    public int Id { get; private set; }
    public string FileName { get; private set; }
    public string OriginalPath { get; private set; }
    public string? PatientName { get; private set; }
    public string? DestinationPath { get; private set; }
    public ReportStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? ImportedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    private Report(){}

    public static Report CreateUploaded(string fileName, string originalPath)
    {
        return new Report
        {
            FileName = fileName,
            OriginalPath = originalPath,
            Status = ReportStatus.UPLOADED,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetId(int id)
    {
        Id = id;
    }

    public void MarkAsProcessed(string patientName)
    {
        PatientName = patientName;
        Status = ReportStatus.PROCESSED;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsImported(string destinationPath)
    {
        DestinationPath = destinationPath;
        Status = ReportStatus.IMPORTED;
        ImportedAt = DateTime.UtcNow;
    }

    public void MarkAsSuccess()
    {
        Status = ReportStatus.SUCCESS;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = ReportStatus.FAILED;
    }
}