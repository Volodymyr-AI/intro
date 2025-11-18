namespace PMSIntegration.Core.Interfaces;

/// <summary>
/// Generic file system operations (not PMS-specific)
/// </summary>
public interface ILocalFileSystemService
{
    /// <summary>
    /// Check if file exists at the specified path
    /// </summary>
    Task<bool> FileExistsAsync(string path);
    
    /// <summary>
    /// Move file from source to destination
    /// </summary>
    /// <returns>Destination path where file was moved</returns>
    Task<string> MoveFileAsync(string sourcePath, string destinationPath);
    
    /// <summary>
    /// Delete file at the specified path
    /// </summary>
    Task DeleteFileAsync(string path);
    
    /// <summary>
    /// Check if directory exists at the specified path
    /// </summary>
    Task<bool> DirectoryExistsAsync(string path);
}