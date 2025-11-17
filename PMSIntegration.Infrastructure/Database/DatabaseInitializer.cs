using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace PMSIntegration.Infrastructure.Database;

public class DatabaseInitializer
{
    private readonly string _databasePath;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string databasePath, ILogger<DatabaseInitializer> logger)
    {
        _databasePath = databasePath;
        _logger = logger;
    }

    public void Initialize()
    {
        var fileExists = File.Exists(_databasePath);

        if (!fileExists)
        {
            SQLiteConnection.CreateFile(_databasePath);
            _logger.LogInformation($"Created new database at: {_databasePath}");
        }
        
        using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
        connection.Open();
        
        CreateTables(connection);
    }
    
    private void CreateTables(SQLiteConnection connection)
    {
        // Patients table with resilience fields
        ExecuteNonQuery(connection, @"
            CREATE TABLE IF NOT EXISTS Patients (
                Id INTEGER PRIMARY KEY,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Gender TEXT,
                Phone TEXT NOT NULL,
                Email TEXT,
                Address TEXT,
                City TEXT,
                State TEXT,
                ZipCode TEXT,
                DateOfBirth TEXT NOT NULL,
                IsSynced BOOLEAN NOT NULL DEFAULT 0,
                ReportReady BOOLEAN NOT NULL DEFAULT 0
            )");
        
        // SyncState table for resilience
        ExecuteNonQuery(connection, @"
            CREATE TABLE IF NOT EXISTS SyncState (
                Id INTEGER PRIMARY KEY,
                LastSuccessfulSync TEXT,
                LastAttemptedSync TEXT,
                FailedAttempts INTEGER DEFAULT 0,
                LastError TEXT,
                Status TEXT NOT NULL
            )");
        
        // Insurance table
        ExecuteNonQuery(connection, @"
            CREATE TABLE IF NOT EXISTS Insurance (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PatientId INTEGER NOT NULL,
                CarrierName TEXT NOT NULL,
                PolicyNumber TEXT NOT NULL,
                GroupNumber TEXT,
                PolicyholderName TEXT NOT NULL,
                Relationship TEXT NOT NULL,
                Priority TEXT NOT NULL,
                FOREIGN KEY (PatientId) REFERENCES Patients (Id)
            )");
        
        // Configuration table
        ExecuteNonQuery(connection, @"
            CREATE TABLE IF NOT EXISTS Configuration (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )");
        
        // Reports table
        ExecuteNonQuery(connection, @"
            CREATE TABLE IF NOT EXISTS Reports (
                Id  INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                PatientId  INTEGER,
                PatientName TEXT,
                SourcePath TEXT NOT NULL,
                DestinationPath TEXT,
                Status TEXT NOT NULL,
                ErrorMessage TEXT,
                CreatedAt TEXT NOT NULL,
                ProcessedAt TEXT,
                ImportedAt TEXT,
                CompletedAt TEXT 
            )");
        
        _logger.LogInformation("Database tables initialized successfully");
    }

    private void SeedConfiguration(SQLiteConnection connection)
    {
        ExecuteNonQuery(connection, @"
            INSERT OR IGNORE INTO Configuration (Key, Value)
            VALUES ('SyncApiBaseUrl', 'https://sync-api.getdentalray.com')");
        
        _logger.LogInformation("Configured with seeded data");
    }

    private void ExecuteNonQuery(SQLiteConnection connection, string sql)
    {
        using var command = new SQLiteCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}