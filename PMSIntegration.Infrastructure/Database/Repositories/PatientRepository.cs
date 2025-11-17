using System.Data.SQLite;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Enums;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.Database.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly DatabaseContext _context;
    private readonly ILogger<PatientRepository> _logger;

    public PatientRepository(DatabaseContext context, ILogger<PatientRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HashSet<int>> GetAllIdsAsync()
    {
        var ids = new HashSet<int>();
        const string sql = "SELECT Id FROM Patients";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
        }
        
        _logger.LogDebug($"Retrieved {ids.Count} patients IDs from database");
        return ids;
    }

    public async Task<Patient?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, FirstName, LastName, Gender, Phone, Email,
                   Address, City, State, ZipCode, DateOfBirth,
                   IsSynced, ReportReady
            FROM Patients
            WHERE Id = @id";

        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapPatientFromReader((SQLiteDataReader)reader);
        }

        return null;
    }

    public async Task<List<Patient>> GetAllAsync()
    {
        var patients = new List<Patient>();
        const string sql = @"
        SELECT Id, FirstName, LastName, Gender, Phone, Email, 
               Address, City, State, ZipCode, DateOfBirth,
               IsSynced, ReportReady
        FROM Patients
        ORDER BY LastName, FirstName";
    
        using var command = new SQLiteCommand(sql, _context.Connection);
        using var reader = await command.ExecuteReaderAsync();
    
        while (await reader.ReadAsync())
        {
            patients.Add(MapPatientFromReader((SQLiteDataReader)reader));
        }
    
        _logger.LogDebug($"Retrieved {patients.Count} patients from database");
        return patients;
    }
    
    public async Task<bool> UpdateAsync(Patient patient)
    {
        const string sql = @"
        UPDATE Patients SET
            FirstName = @firstName,
            LastName = @lastName,
            Gender = @gender,
            Phone = @phone,
            Email = @email,
            Address = @address,
            City = @city,
            State = @state,
            ZipCode = @zipCode,
            DateOfBirth = @dateOfBirth,
            IsSynced = @isSynced,
            ReportReady = @reportReady
        WHERE Id = @id";
    
        using var command = new SQLiteCommand(sql, _context.Connection);
    
        command.Parameters.AddWithValue("@id", patient.Id);
        command.Parameters.AddWithValue("@firstName", patient.FirstName);
        command.Parameters.AddWithValue("@lastName", patient.LastName);
        command.Parameters.AddWithValue("@gender", patient.Gender);
        command.Parameters.AddWithValue("@phone", patient.Phone);
        command.Parameters.AddWithValue("@email", patient.Email ?? "");
        command.Parameters.AddWithValue("@address", patient.Address ?? "");
        command.Parameters.AddWithValue("@city", patient.City ?? "");
        command.Parameters.AddWithValue("@state", patient.State ?? "");
        command.Parameters.AddWithValue("@zipCode", patient.ZipCode ?? "");
        command.Parameters.AddWithValue("@dateOfBirth", patient.DateOfBirth.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@isSynced", patient.IsSynced);
        command.Parameters.AddWithValue("@reportReady", patient.ReportReady);
    
        var affected = await command.ExecuteNonQueryAsync();
    
        if (affected > 0)
        {
            _logger.LogDebug($"Updated patient: {patient.FirstName} {patient.LastName} (ID: {patient.Id})");
        }
    
        return affected > 0;
    }

    public async Task<List<Patient>> GetUnsyncedAsync(int limit = 100)
    {
        var patients = new List<Patient>();
        const string sql = @"
            SELECT Id, FirstName, LastName, Gender, Phone, Email, 
                   Address, City, State, ZipCode, DateOfBirth,
                   IsSynced, ReportReady
            FROM Patients
            WHERE IsSynced = 0
            LIMIT @limit";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        command.Parameters.AddWithValue("@limit", limit);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            patients.Add(MapPatientFromReader((SQLiteDataReader)reader));
        }
        
        return patients;
    }

    public async Task<int> BulkSaveAsync(List<Patient> patients)
    {
        if (!patients.Any())
            return 0;
        
        const int batchSize = 50;
        int totalSaved = 0;

        for (int i = 0; i < patients.Count; i += batchSize)
        {
            var batch = patients.Skip(i).Take(batchSize).ToList();

            try
            {
                var savedCount = await SaveBatchAsync(batch);
                totalSaved += savedCount;

                _logger.LogInformation(
                    $"Progress: Saved batch {(i / batchSize) + 1} of {Math.Ceiling((decimal)patients.Count / batchSize)} ({totalSaved}/{patients.Count} total)"); 

                await Task.Delay(100);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("database is locked"))
            {
                _logger.LogWarning($"Database locked on batch {i / batchSize}, retrying...");
                await Task.Delay(500);
                i -= batchSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving batch {i / batchSize}");
                throw;
            }
        }
        return totalSaved;
    }

    private async Task<int> SaveBatchAsync(List<Patient> batch)
    {
        const string sql = @"
            INSERT OR REPLACE INTO Patients(
                   Id, FirstName, LastName, Gender, Phone, Email,
                   Address, City, State, ZipCode, DateOfBirth,
                   IsSynced, ReportReady
            ) VALUES (
                   @id, @firstName, @lastName, @gender, @phone, @email, @address,
                   @city, @state, @zipCode, @dateOfBirth, @isSynced, @reportReady
                   )";

        return await _context.ExecuteInTransactionAsync(async (transaction) =>
        {
            var count = 0;
            foreach (var patient in batch)
            {
                using var command = new SQLiteCommand(sql, _context.Connection, transaction);

                command.Parameters.AddWithValue("@id", patient.Id);
                command.Parameters.AddWithValue("@firstName", patient.FirstName);
                command.Parameters.AddWithValue("@lastName", patient.LastName);
                command.Parameters.AddWithValue("@gender", patient.Gender);
                command.Parameters.AddWithValue("@phone", patient.Phone);
                command.Parameters.AddWithValue("@email", patient.Email ?? "");
                command.Parameters.AddWithValue("@address", patient.Address ?? "");
                command.Parameters.AddWithValue("@city", patient.City ?? "");
                command.Parameters.AddWithValue("@state", patient.State ?? "");
                command.Parameters.AddWithValue("@zipCode", patient.ZipCode ?? "");
                command.Parameters.AddWithValue("@dateOfBirth", patient.DateOfBirth.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@isSynced", true);
                command.Parameters.AddWithValue("@reportReady", patient.ReportReady);

                await command.ExecuteNonQueryAsync();
                count++;
            }

            return count;
        });
    }

    public async Task<bool> MarkAsSyncedAsync(List<int> patientIds)
    {
        if (!patientIds.Any()) return true;
        
        var idList = string.Join(",", patientIds);
        var sql = $"UPDATE Patients SET IsSynced = 1  WHERE Id IN ({idList})";
         using var command = new SQLiteCommand(sql, _context.Connection);
         var affected = await command.ExecuteNonQueryAsync();

         return affected > 0;
    }

    public async Task<SyncState?> GetSyncStateAsync()
    {
        const string sql = @"
            SELECT LastSuccessfulSync, LastAttemptedSync, FailedAttempts,
                   LastError, Status
            FROM SyncState
            WHERE Id = 1";
        
        using var command = new SQLiteCommand(sql, _context.Connection);
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new SyncState
            {
                LastSuccessfulSync = reader.IsDBNull(0) ? null : DateTime.Parse(reader.GetString(0)),
                LastAttemptedSync = reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1)),
                FailedAttempts = reader.GetInt32(2),
                LastError = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = Enum.Parse<Core.Enums.SyncStatus>(reader.GetString(4))
            };
        }
        
        return null;
    }

    public async Task UpdateSyncStateAsync(SyncState state)
    {
        const string sql = @"
            INSERT OR REPLACE INTO SyncState (
                   Id, LastSuccessfulSync, LastAttemptedSync, 
                   FailedAttempts, LastError, Status
            ) VALUES (
                   1, @lastSuccess, @lastAttempt, @failures, @error, @status 
            )";

        using var command = new SQLiteCommand(sql, _context.Connection);
        
        command.Parameters.AddWithValue("@lastSuccess", 
            state.LastSuccessfulSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@lastAttempt", 
            state.LastAttemptedSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@failures", state.FailedAttempts);
        command.Parameters.AddWithValue("@error", state.LastError ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@status", state.Status.ToString());
        
        await command.ExecuteNonQueryAsync();
    }

    private Patient MapPatientFromReader(SQLiteDataReader reader)
    {
        return new Patient
        {
            Id = reader.GetInt32(0),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            Gender = reader.GetString(3),
            Phone = reader.GetString(4),
            Email = reader.IsDBNull(4) ? "" : reader.GetString(5),
            Address = reader.IsDBNull(5) ? "" : reader.GetString(6),
            City = reader.IsDBNull(6) ? "" : reader.GetString(7),
            State = reader.IsDBNull(7) ? "" : reader.GetString(8),
            ZipCode = reader.IsDBNull(8) ? "" : reader.GetString(9),
            DateOfBirth = DateTime.Parse(reader.GetString(10)),
            IsSynced = reader.GetBoolean(11),
            ReportReady = reader.GetBoolean(12)
        };
    }
}