using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Entities;
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

    public async Task<int> InsertAsync(Report report)
    {
        const string sql = @"
            INSERT INTO Reports(
                FileName, PatientName, SourcePath,
                DestinationPath, Status, ErrorMessage,
                CreatedAt, ProcessedAt, ImportedAt, CompletedAt
            ) VALUES (
                @fileName, @patientName, @sourcePath,
                @destinationPath, @status, @errorMessage,
                @createdAt, @processedAt, @importedAt, @completedAt
            );";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        
        command.Parameters.AddWithValue("@fileName", report.FileName);
        command.Parameters.AddWithValue("@patientName", (object?)report.PatientName ?? DBNull.Value);
        command.Parameters.AddWithValue("@sourcePath", report.SourcePath);
        command.Parameters.AddWithValue("@destinationPath", (object?)report.DestinationPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", report.Status.ToString());
        command.Parameters.AddWithValue("@errorMessage", (object?)report.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", report.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@processedAt", 
            report.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@importedAt", 
            report.ImportedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@completedAt", 
            report.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        
        var result = await command.ExecuteScalarAsync();
        var id = Convert.ToInt32(result);
        
        _logger.LogDebug($"Inserted report: {report.FileName} with ID: {id}");
        return id;
    }
    
    public async Task<Report?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, FileName, PatientName, SourcePath,
                   DestinationPath, Status, ErrorMessage,
                   CreatedAt, ProcessedAt, ImportedAt, CompletedAt
            FROM Reports WHERE Id = @id";

        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReportFromReader((SQLiteDataReader)reader);
        }

        return null;
    }
    public async Task<List<Report>> GetByStatusAsync(ReportStatus status)
    {
        var reports = new List<Report>();
        const string sql = @"
            SELECT Id, FileName, PatientName, SourcePath,
                   DestinationPath, Status, ErrorMessage,
                   CreatedAt, ProcessedAt, ImportedAt, CompletedAt
            FROM Reports 
            WHERE Status = @status
            ORDER BY CreatedAt ASC";

        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@status", status.ToString());

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reports.Add(MapReportFromReader((SQLiteDataReader)reader));
        }

        return reports;
    }

    public async Task<bool> UpdateAsync(Report report)
    {
        const string sql = @"
            UPDATE Reports SET
                FileName = @fileName,
                PatientName = @patientName,
                SourcePath = @sourcePath,
                DestinationPath = @destinationPath,
                Status = @status,
                ErrorMessage = @errorMessage,
                ProcessedAt = @processedAt,
                ImportedAt = @importedAt,
                CompletedAt = @completedAt
            WHERE Id = @id";

        using var command = new SQLiteCommand(sql, _context.Connection);
        
        command.Parameters.AddWithValue("@id", report.Id);
        command.Parameters.AddWithValue("@fileName", report.FileName);
        command.Parameters.AddWithValue("@patientName", (object?)report.PatientName ?? DBNull.Value);
        command.Parameters.AddWithValue("@sourcePath", report.SourcePath);
        command.Parameters.AddWithValue("@destinationPath", (object?)report.DestinationPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", report.Status.ToString());
        command.Parameters.AddWithValue("@errorMessage", (object?)report.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@processedAt", 
            report.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@importedAt", 
            report.ImportedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@completedAt", 
            report.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);

        var affected = await command.ExecuteNonQueryAsync();
        return affected > 0;
    }

    private Report MapReportFromReader(SQLiteDataReader reader)
    {
        return new Report
        {
            Id = reader.GetInt32(0),
            FileName = reader.GetString(1),
            PatientName = reader.IsDBNull(2) ? null : reader.GetString(2),
            SourcePath = reader.GetString(3),
            DestinationPath = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status = Enum.Parse<ReportStatus>(reader.GetString(5)),
            ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            ProcessedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
            ImportedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            CompletedAt = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10))
        };
    }
}