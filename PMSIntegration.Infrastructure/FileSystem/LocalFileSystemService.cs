using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.FileSystem;

/// <summary>
/// Generic file system operations (not PMS-specific)
/// </summary>
/// 
public class LocalFileSystemService : ILocalFileSystemService
{
    private readonly ILogger<LocalFileSystemService> _logger;

    public LocalFileSystemService(ILogger<LocalFileSystemService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        return await Task.Run(() => File.Exists(path));
    }

    public async Task<string> MoveFileAsync(string sourcePath, string destinationPath)
    {
        if (!await FileExistsAsync(sourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
            _logger.LogInformation($"Created directory: {destinationDir}");
        }

        await Task.Run(() => File.Move(sourcePath, destinationPath, overwrite: false));
        _logger.LogDebug($"Moved file: {sourcePath} -> {destinationPath}");
        
        return destinationPath;
    }

    public async Task DeleteFileAsync(string sourcePath)
    {
        if (!await FileExistsAsync(sourcePath))
        {
            _logger.LogWarning($"File not found: {sourcePath}");
            return;
        }
        
        await Task.Run(() => File.Delete(sourcePath));
        _logger.LogDebug($"Deleted file: {sourcePath}");
    }

    public async Task<bool> DirectoryExistsAsync(string path)
    {
        return await Task.Run(() =>  Directory.Exists(path));
    }
}