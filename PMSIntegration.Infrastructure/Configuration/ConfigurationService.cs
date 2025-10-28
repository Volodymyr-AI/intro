using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PMSIntegration.Infrastructure.Database;
using PMSIntegration.Infrastructure.PmsAdapter.OpenDental;

namespace PMSIntegration.Infrastructure.Configuration;

public class ConfigurationService
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ConfigurationService> _logger;
    
    public ConfigurationService(DatabaseContext context, ILogger<ConfigurationService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<string?> GetAsync(string key)
    {
        const string sql = "SELECT Value FROM Configuration WHERE Key = @key";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@key", key);
        
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }
    
    public async Task SetAsync(string key, string value)
    {
        const string sql = @"
            INSERT OR REPLACE INTO Configuration (Key, Value, UpdatedAt) 
            VALUES (@key, @value, @updatedAt)";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        
        await command.ExecuteNonQueryAsync();
        _logger.LogDebug($"Configuration updated: {key}");
    }
    
    public async Task SaveOpenDentalConfigAsync(OpenDentalConfiguration config)
    {
        await SetAsync("AuthScheme", config.AuthScheme);
        await SetAsync("AuthToken", config.AuthToken);
        await SetAsync("ApiBaseUrl", config.ApiBaseUrl);
        await SetAsync("TimeoutSeconds", config.TimeoutSeconds.ToString());
        
        if (!string.IsNullOrEmpty(config.ImageFolderPath))
        {
            await SetAsync("OpenDental.ImageFolderPath", config.ImageFolderPath);
        }
        
        _logger.LogInformation("OpenDental configuration saved");
    }
}