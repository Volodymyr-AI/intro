namespace PMSIntegration.Core.Interfaces;

public interface IPmsFileSystemService
{
    /// <summary>
    /// Find patient folder path by patient name
    /// OpenDental: C:\OpenDentImages\A\AllowedAllen11
    /// Dentrix: C:\Dentrix\PatientImages\{PatientId}
    /// </summary>
    Task<string?> FindPatientFolderAsync(string patientName, int patientId);
    
    /// <summary>
    /// Get fallback Reports folder path if patient folder not found
    /// OpenDental: C:\OpenDentImages\Reports
    /// </summary>
    string GetReportsFallbackPath();
    
    /// <summary>
    /// Move file to destination with PMS-specific naming conventions
    /// </summary>
    Task<string> MoveReportToPatientFolderAsync(string sourceFile, string destinationFolder, string fileName);
    
    /// <summary>
    /// Validate that PMS image folder is accessible
    /// </summary>
    Task<bool> ValidateImageFolderAccessAsync();
}