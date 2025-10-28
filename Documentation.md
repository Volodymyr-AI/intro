# PMSIntegration - Patient Management System Integration

## 📋 Overview

PMSIntegration is a Windows background service that integrates with various Patient Management Systems (PMS) to export patient data and store it in a local SQLite database. Currently supports OpenDental with plans for Dentrix and EagleSoft integration.

## 🏗️ Architecture

The project follows Clean Architecture principles with clear separation of concerns:

```
PMSIntegration/
├── PMSIntegration.Core/          # Domain layer (entities, value objects)
├── PMSIntegration.Application/   # Application layer (interfaces, configuration)
├── PMSIntegration.Infrastructure/ # Infrastructure layer (database, external APIs)
└── PMSIntegration.Worker/        # Presentation layer (Windows Service)
```

### Key Components

- **Domain Models**: Patient entity with value objects
- **Configuration Management**: INI-based configuration with database backup
- **Background Service**: Automated patient data synchronization
- **Database**: SQLite for local data storage
- **Logging**: File-based logging system
- **ServiceHub**: Centralized service utilities and path management
- **PMS Integration**: Modular design supporting multiple PMS providers

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- Windows OS (for Windows Service functionality)
- OpenDental API access (for OpenDental integration)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://<your_bit_bucket>@bitbucket.org/dentalray/pmsintegration.git
   cd pmsintegration
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Configure the application**
   Edit `PMSIntegration.Worker/Configuration.ini`:
   ```ini
   [Integration]
   Provider = OpenDental
   ExportStartDate = 2000-01-01

   [OpenDental]
   AuthScheme = ODFHIR
   AuthToken = your_auth_token_here
   ApiBaseUrl = http://localhost:30222
   TimeoutSeconds = 30
   ```

## ⚙️ Configuration

### Configuration.ini Structure

```ini
[Integration]
# Available providers: OpenDental, Dentrix, EagleSoft
Provider = OpenDental

# Patient data export start date (yyyy-MM-dd)
ExportStartDate = 2025-01-01

[OpenDental]
# Authorization for OpenDental API
AuthScheme = ODFHIR
AuthToken = your_auth_token_here

# OpenDental API settings
ApiBaseUrl = http://localhost:30222
TimeoutSeconds = 30
```

### Configuration Parameters

| Parameter | Description | Example                              |
|-----------|-------------|--------------------------------------|
| `Provider` | PMS system type | `OpenDental`, `Dentrix`, `EagleSoft` |
| `ExportStartDate` | Date to start patient export from | `2000-01-01`                         |
| `DbPath` | SQLite database file name | `database.db`                        |
| `AuthScheme` | API authentication scheme | `ODFHIR`, `Bearer`                   |
| `AuthToken` | API authentication token | `NFF6i0KrXrxDkZHt...`                |
| `ApiBaseUrl` | PMS API base URL | `http://localhost:30222`             |
| `TimeoutSeconds` | API request timeout | `30`                                 |

## 🧪 Testing

### Development Testing

1. **Start the application in development mode**
   ```bash
   cd PMSIntegration.Worker
   dotnet run
   
   # Alternative - run from solution root
   dotnet run --project PMSIntegration.Worker
   ```

2. **Verify file locations**
   In development mode, files are created in the Worker project root:
   ```
   PMSIntegration.Worker/
   ├── database.db          # SQLite database
   ├── Logs/               # Log files
   │   ├── info.log        # General information
   │   ├── error.log       # Error messages
   │   ├── warn.log        # Warnings
   │   └── debug.log       # Debug information
   └── Configuration.ini
   ```

3. **Check logs for proper operation**
   ```bash
   # View info logs
   Logs/info.log
   # View error logs (if any)
   Logs/error.log
   ```

### Testing OpenDental Integration

1. **Ensure OpenDental is running**
   - Start OpenDental application
   - Verify API is accessible at configured URL
   - Confirm authentication token is valid

2. **Test API connectivity**
   ```bash
   # Test API endpoint manually
   curl -H "Authorization: ODFHIR your_token_here" http://localhost:30222/api/v1/patients
   ```

3. **Monitor patient import**
   - Check info.log for patient export messages
   - Verify database.db is created and populated
   - Confirm patient count matches expectations

### Database Verification

1. **Install SQLite tools** (optional)
   ```bash
   # Windows
   winget install SQLite.SQLite
   
   # Or download from https://sqlite.org/download.html
   ```

2. **Inspect database**
   ```sql
   -- Open database
   sqlite3 database.db
   
   -- Check tables
   .tables
   
   -- View patients
   SELECT COUNT(*) FROM Patients;
   SELECT * FROM Patients LIMIT 5;
   
   -- Check configuration
   SELECT * FROM Config;
   
   -- Exit
   .quit
   ```

### Production Testing

1. **Build release version**
   ```bash
   dotnet publish -c Release
   ```

2. **Install as Windows Service**
   ```bash
   # Run as Administrator
   cd PMSIntegration.Worker/bin/Release/net8.0/publish
   install.bat
   ```

3. **Verify service status**
   ```bash
   # Check service status
   sc query PMSIntegration
   
   # View service logs
   # Files will be in installation directory
   ```

## 🔧 Development

### Project Structure

```
PMSIntegration.Core/
├── Configuration/
│   ├── Abstract/IntegrationConfig.cs
│   ├── Enum/PmsProvider.cs
│   └── Models/OpenDentalIntegrationConfig.cs
└── Patients/
    ├── Models/Patient.cs
    └── ValueObjects/PatientId.cs

PMSIntegration.Application/
├── Configuration/
│   ├── IniConfigurationService.cs
│   └── IniParser.cs
├── Hub/
│   └── ServiceHub.cs           # Centralized utilities and path management
├── Logging/
│   ├── FileLogger.cs
│   └── LogLevel.cs
└── Interfaces/IStartupService.cs

PMSIntegration.Infrastructure/
├── Database/
│   ├── Config/ConfigTable.cs
│   ├── Patient/PatientTable.cs
│   └── DatabaseInitializer.cs
├── OpenDental/
│   ├── DTO/OpenDentalPatientDto.cs
│   ├── Extensions/OpenDentalPatientMapper.cs
│   └── Services/OpenDentalService.cs
└── Services/PatientExportService.cs

PMSIntegration.Worker/
├── Program.cs                  # Entry point and DI configuration
├── Worker.cs                   # Background service implementation
├── StartupService.cs           # Database initialization
└── Configuration.ini
```

### ServiceHub - Central Utilities

The `ServiceHub` class provides centralized utilities and path management for the entire application:

```csharp
// PMSIntegration.Application.Hub.ServiceHub
public static class ServiceHub 
{
    // Get database path based on environment
    public static string GetDatabasePath(string configDbPath)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development")
        {
            return Path.Combine(Directory.GetCurrentDirectory(), configDbPath);
        }
        return Path.Combine(AppContext.BaseDirectory, configDbPath);
    }
    
    // Future extensions:
    // GetLogsPath(), GetReportsPath(), IsDevelopment(), etc.
}
```

**Benefits of ServiceHub:**
- **Centralized Logic**: Single source of truth for environment-specific behavior
- **Extensibility**: Easy to add new utility methods as the application grows
- **Consistency**: Ensures all components use the same path resolution logic
- **Testability**: Easy to mock and test environment-specific behavior

### Adding New PMS Provider

1. **Create configuration model**
   ```csharp
   // PMSIntegration.Core/Configuration/Models/DentrixIntegrationConfig.cs
   public class DentrixIntegrationConfig : IntegrationConfig
   {
       public string DatabasePath { get; set; } = "";
       public string UserId { get; set; } = "";
       // Add Dentrix-specific properties
   }
   ```

2. **Create DTO and mapper**
   ```csharp
   // PMSIntegration.Infrastructure/Dentrix/DTO/DentrixPatientDto.cs
   // PMSIntegration.Infrastructure/Dentrix/Extensions/DentrixPatientMapper.cs
   ```

3. **Create service**
   ```csharp
   // PMSIntegration.Infrastructure/Dentrix/Services/DentrixService.cs
   ```

4. **Register in Program.cs**
   ```csharp
   case PmsProvider.Dentrix:
       services.AddScoped<DentrixService>();
       break;
   ```

### Database Schema

#### Patients Table
```sql
CREATE TABLE Patients (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Phone TEXT NOT NULL,
    Email TEXT,
    Address TEXT NOT NULL,
    City TEXT NOT NULL,
    State TEXT NOT NULL,
    ZipCode TEXT NOT NULL,
    DataExported BOOLEAN NOT NULL,
    BirthDate TEXT
);
```

#### Config Table
```sql
CREATE TABLE Config (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

## 🚀 Deployment

### Windows Service Installation

1. **Publish the application**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Copy files to target machine**
   ```
   C:\Program Files\PMSIntegration\
   ├── PMSIntegration.Worker.exe
   ├── Configuration.ini
   ├── *.dll files
   ├── install.bat
   ├── start.bat
   ├── stop.bat
   └── uninstall.bat
   ```

3. **Install service** (Run as Administrator)
   ```bash
   install.bat
   ```

4. **Manage service**
   ```bash
   # Start service
   start.bat
   
   # Stop service
   stop.bat
   
   # Uninstall service
   uninstall.bat
   ```

### Service Management

```bash
# Manual service commands
sc create PMSIntegration binPath="C:\Program Files\PMSIntegration\PMSIntegration.Worker.exe"
sc start PMSIntegration
sc stop PMSIntegration
sc delete PMSIntegration

# Check service status
sc query PMSIntegration

# View service configuration
sc qc PMSIntegration
```

## 📊 Monitoring

### Log Files

The application creates separate log files for different log levels:

- **info.log**: General application information, patient export status
- **error.log**: Error messages and exceptions
- **warn.log**: Warning messages
- **debug.log**: Detailed debugging information

### Log Monitoring

```bash
# Real-time log monitoring (PowerShell)
Get-Content -Path "Logs\info.log" -Wait -Tail 10

# Search for errors
Select-String -Path "Logs\error.log" -Pattern "Error"

# View recent entries
Get-Content -Path "Logs\info.log" -Tail 50
```

### Performance Metrics

Monitor these key metrics:

- **Patient export frequency**: Default 60 minutes
- **Database size growth**: Monitor database.db file size
- **API response times**: Check for timeout errors
- **Memory usage**: Monitor service memory consumption

## 🔍 Troubleshooting

### Common Issues

1. **Service won't start**
   - Check Windows Event Viewer
   - Verify Configuration.ini exists and is valid
   - Ensure all dependencies are installed

2. **Database connection errors**
   - Check file permissions
   - Verify database path in configuration
   - Ensure SQLite runtime is available

3. **API connection failures**
   - Verify PMS system is running
   - Check API URL and authentication
   - Review firewall settings

4. **No patients imported**
   - Verify ExportStartDate is correct
   - Check PMS system has patients after that date
   - Review API authentication

### Debug Steps

1. **Enable debug logging**
   - Temporarily set log level to Debug
   - Review debug.log for detailed information

2. **Test API manually**
   ```bash
   curl -H "Authorization: ODFHIR your_token" http://localhost:30222/api/v1/patients
   ```

3. **Check database**
   ```sql
   sqlite3 database.db
   SELECT COUNT(*) FROM Patients;
   SELECT * FROM Config;
   ```

4. **Review service logs**
   ```bash
   # Windows Event Viewer
   eventvwr.msc
   # Navigate to Windows Logs > Application
   # Filter by Source: PMSIntegration
   ```

## 📚 API Reference

### OpenDental API Endpoints

- **GET /api/v1/patients**: Get all patients
- **GET /api/v1/patients/Simple**: Get simplified patient data
- **GET /api/v1/patients?PatNum={id}**: Get specific patient

### Configuration API

The application stores configuration in both INI file and database:

```csharp
// Access configuration programmatically
var configService = ConfigServiceFactory.Create();
var apiUrl = configService.GetParam("ApiBaseUrl", "http://localhost:30222");
configService.SetParam("LastExportTime", DateTime.Now.ToString());
```

### ServiceHub API

Central utilities for path management and environment detection:

```csharp
// Get environment-specific database path
var dbPath = ServiceHub.GetDatabasePath("database.db");

// Check if running in development
var isDev = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
```

## 🤝 Contributing

1. **Fork the repository**
2. **Create feature branch**: `git switch -b feature/new-pms-provider`
3. **Create fix branch**: `git switch -b fix/new-pms-provider`
4. **Make changes**: Follow existing code patterns
5. **Add tests**: Ensure new functionality is tested
6. **Submit pull request**: Include description of changes

### Code Standards

- Follow C# naming conventions
- Use dependency injection
- Add appropriate logging
- Handle exceptions gracefully
- Write clear, self-documenting code

## 📄 License

[TODO: Add license information here]

---

**Version**: 1.0.0  
**Last Updated**: 18.08.2025 (dd/mm/yyyy)