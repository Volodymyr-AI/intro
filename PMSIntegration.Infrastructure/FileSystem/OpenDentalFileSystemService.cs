using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Interfaces;
using PMSIntegration.Infrastructure.PmsAdapter.OpenDental;

namespace PMSIntegration.Infrastructure.FileSystem;

public class OpenDentalFileSystemService : IPmsFileSystemService
{
    private readonly OpenDentalConfiguration _config;
    private readonly LocalFileSystemService _fileSystem;
    private readonly ILogger<OpenDentalFileSystemService> _logger;

    public OpenDentalFileSystemService(
        OpenDentalConfiguration config, 
        LocalFileSystemService fileSystem,
        ILogger<OpenDentalFileSystemService> logger)
    {
        _config = config;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<string?> FindPatientFolderAsync(string patientName, int patientId)
    {
        if (string.IsNullOrWhiteSpace(_config.ImageFolderPath))
        {
            _logger.LogError("ImageFolderPath not configured");
            return null;
        }
        
        // Parse patient name: "Allowed Allen" or "Allen Allowed" 
        // We need LastName + FirstName for folder: "AllowedAllen11"
        // Try both combinations since we're not sure of the order in patientName
        var nameParts = patientName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length < 2)
        {
            _logger.LogWarning($"Invalid patient name format: {patientName}");
            return null;
        }

        // Try LastName FirstName (most common from filename: "Allowed Allen.pdf")
        var lastFirst = $"{nameParts[0]}{nameParts[1]}";
        var firstLetter = nameParts[0][0].ToString().ToUpper();
        var folderName = $"{lastFirst}{patientId}";
        var patientFolderPath = Path.Combine(_config.ImageFolderPath, firstLetter, folderName);

        if (await _fileSystem.DirectoryExistsAsync(patientFolderPath))
        {
            _logger.LogInformation($"Found patient folder: {patientFolderPath}");
            return patientFolderPath;
        }

        // Try FirstName LastName as fallback
        var firstLast = $"{nameParts[1]}{nameParts[0]}";
        firstLetter = nameParts[1][0].ToString().ToUpper();
        folderName = $"{firstLast}{patientId}";
        patientFolderPath = Path.Combine(_config.ImageFolderPath, firstLetter, folderName);

        if (await _fileSystem.DirectoryExistsAsync(patientFolderPath))
        {
            _logger.LogInformation($"Found patient folder: {patientFolderPath}");
            return patientFolderPath;
        }

        _logger.LogWarning($"Patient folder not found for: {patientName} (ID: {patientId})");
        return null;
    }

    public string GetReportsFallbackPath()
    {
        if (string.IsNullOrWhiteSpace(_config.ImageFolderPath))
        {
            throw new InvalidOperationException("ImageFolderPath not configured");
        }
        
        // Example: C:\OpenDentImages\Reports
        return Path.Combine(_config.ImageFolderPath, "Reports");
    }

    public async Task<string> MoveReportToPatientFolderAsync(
        string sourceFile, 
        string destinationFolder, 
        string fileName)
    {
        var destinationPath = Path.Combine(destinationFolder, fileName);

        // Check if file already exists with same name
        if (await _fileSystem.FileExistsAsync(destinationPath))
        {
            // Add timestamp to avoid conflicts
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            fileName = $"{nameWithoutExt}_{timestamp}{extension}";
            destinationPath = Path.Combine(destinationFolder, fileName);
        }
        
        return await _fileSystem.MoveFileAsync(sourceFile, destinationPath);
    }

    public async Task<bool> ValidateImageFolderAccessAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.ImageFolderPath))
        {
            _logger.LogError("ImageFolderPath not configured");
            return false;
        }
        
        if (!await _fileSystem.DirectoryExistsAsync(_config.ImageFolderPath))
        {
            _logger.LogError($"ImageFolderPath does not exist: {_config.ImageFolderPath}");
            return false;
        }

        var reportsPath = GetReportsFallbackPath();
        if (!await _fileSystem.DirectoryExistsAsync(reportsPath))
        {
            try
            {
                Directory.CreateDirectory(reportsPath);
                _logger.LogInformation($"Created Reports folder: {reportsPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create Reports folder: {reportsPath}");
                return false;
            }
        }

        return true;
    }
}