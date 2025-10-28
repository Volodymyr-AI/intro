namespace PMSIntegration.Core.Reports.Enums;

public enum ReportStatus
{
    /// <summary>
    /// File has been uploaded to Reports folder
    /// </summary>
    UPLOADED,
    
    /// <summary>
    /// Processed report - took all needed data and added to db
    /// </summary>
    PROCESSED,
    
    /// <summary>
    /// РReport imported successfully into PMS 
    /// </summary>
    IMPORTED,
    
    /// <summary>
    /// Report successfully processed and been deleted from Reports folder
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Error on any of the stages
    /// </summary>
    FAILED
}