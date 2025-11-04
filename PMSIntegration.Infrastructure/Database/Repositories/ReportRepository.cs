using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Enums;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.Database.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ReportRepository> _logger;

    public ReportRepository(DatabaseContext context, ILogger<ReportRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<int> InsertUploadedReport(
        SQLiteConnection connection,
        string fileName)
    {
        const string sql = @"
            INSERT INTO Reports(
                    FileName, Status, CreatedAt            
            ) VALUES (
                      @fileName, @status, @createdAt
            )";
        using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddWithValue("@fileName", fileName);
        command.Parameters.AddWithValue("@status", ReportStatus.UPLOADED.ToString());
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd"));

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}