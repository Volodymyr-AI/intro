using PMSIntegration.Application.Services;
using PMSIntegration.Core.Interfaces;
using PMSIntegration.Infrastructure.Configuration;
using PMSIntegration.Infrastructure.Database;
using PMSIntegration.Infrastructure.Database.Repositories;
using PMSIntegration.Infrastructure.FileSystem;
using PMSIntegration.Infrastructure.PmsAdapter.OpenDental;
using PMSIntegration.Infrastructure.Resilience;
using PMSIntegration.Worker.Workers;
using Serilog;
using Serilog.Events;

namespace PMSIntegration.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // =========================== SERILOG ===========================
            // Configure Serilog for better logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                // Debug logs
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug)
                    .WriteTo.File(
                        Path.Combine("logs", "debug.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                // Info logs
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
                    .WriteTo.File(
                        Path.Combine("logs", "info.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                // Warning logs
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
                    .WriteTo.File(
                        Path.Combine("logs", "warn.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                // Error logs
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.File(
                        Path.Combine("logs", "error.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                // Fatal logs
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Fatal)
                    .WriteTo.File(
                        Path.Combine("logs", "fatal.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        outputTemplate:"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .CreateLogger();
            
            // =========================== MAIN PART ===========================
            try
            {
                Log.Information("Starting PMS Integration Service");

                var builder = Host.CreateApplicationBuilder(args);

                ConfigureServices(builder.Services, builder.Configuration);

                var host = builder.Build();

                // Initialize database on startup
                await InitializeDatabaseAsync(host.Services);
                CreateReportDirectory(builder.Configuration);

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        // =========================== SERVICE DI ===========================
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add Serilog
            services.AddSerilog();
            
            //Configure database path
            var dataDirectory = configuration["DataDirectory"] ?? "data";
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
            var databasePath = Path.Combine(dataDirectory, "database.db");
            
            // Register Database Context
            services.AddSingleton<DatabaseContext>(provider 
                => new DatabaseContext(databasePath));
            
            // Register Database Initializer
            services.AddSingleton<DatabaseInitializer>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<DatabaseInitializer>>();
                return new DatabaseInitializer(databasePath, logger);
            });
            
            // Register Configuration Service
            services.AddSingleton<ConfigurationService>();
            
            // Determine which pms provider to use
            var pmsProvider  = configuration["PmsProvider"] ?? "OpenDental";
            
            // Register appropriate configuration based on provider
            switch (pmsProvider.ToLower())
            {
                case "opendental":
                    RegisterOpenDentalServices(services, configuration);
                    break;
            
                case "dentrix":
                    RegisterDentrixServices(services, configuration);
                    break;
            
                case "eaglesoft":
                    RegisterEagleSoftServices(services, configuration);
                    break;
            
                default:
                    throw new NotSupportedException($"PMS provider '{pmsProvider}' is not supported");
            }
            
            services.AddSingleton<ISyncConfiguration>(provider => 
               provider.GetRequiredService<OpenDentalConfiguration>());
            
            // File System Services
            services.AddScoped<LocalFileSystemService>();
            services.AddScoped<ILocalFileSystemService>(provider => 
                provider.GetRequiredService<LocalFileSystemService>());
            services.AddScoped<OpenDentalFileSystemService>();
            services.AddScoped<IPmsFileSystemService>(provider => 
                provider.GetRequiredService<OpenDentalFileSystemService>());
            
            // Register Repositories
            services.AddScoped<IPatientRepository, PatientRepository>();
            services.AddScoped<IInsuranceRepository, InsuranceRepository>();
            services.AddScoped<IReportRepository, ReportRepository>();
            
            // Register PMS API Service
            services.AddScoped<IPmsApiService, OpenDentalApiService>(provider =>
            {
                var odConfig = provider.GetRequiredService<OpenDentalConfiguration>();
                var logger = provider.GetRequiredService<ILogger<OpenDentalApiService>>();
                var service = new OpenDentalApiService(odConfig, logger);

                return service;
            });
            
            //Register Resilience Policy
            services.AddSingleton<IResiliencePolicy, PollyResiliencePolicy>();
            
            //Register Application Services
            services.AddScoped<ResilientPatientSyncService>();
            services.AddScoped<ResilientReportProcessingService>();
            
            // Register Background Workers
            services.AddHostedService<PatientWorker>();
            services.AddHostedService<ReportWorker>();
            
            // Configure Host Options
            services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(30);
            });
        }
        
        private static async Task InitializeDatabaseAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            
            Log.Information("Initializing database...");
            initializer.Initialize();
            
            // Load and apply any saved configuration
            var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();
            var odConfig = scope.ServiceProvider.GetRequiredService<OpenDentalConfiguration>();
            
            // Try to load saved OpenDental configuration
            var savedAuthToken = await configService.GetAsync("OpenDental.AuthToken");
            if (!string.IsNullOrEmpty(savedAuthToken))
            {
                odConfig.AuthToken = savedAuthToken;
                Log.Information("Loaded saved OpenDental configuration");
            }
            else
            {
                // Save initial configuration if not exists
                await configService.SaveOpenDentalConfigAsync(odConfig);
                Log.Information("Saved initial OpenDental configuration");
            }
        }

        private static void RegisterOpenDentalServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<OpenDentalConfiguration>(provider =>
            {
                var config = configuration.GetSection("OpenDental");
                var odConfig = new OpenDentalConfiguration
                {
                    AuthScheme = config["AuthScheme"] ?? "ODFHIR",
                    AuthToken = config["AuthToken"] ?? "",
                    ApiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:30222",
                    TimeoutSeconds = int.Parse(config["TimeoutSeconds"] ?? "30"),
                    ImageFolderPath = config["ImageFolderPath"],
                    ExportStartDate = DateTime.Parse(config["ExportStartDate"] ?? "2000-01-01")
                };
                return odConfig;
            });

            services.AddSingleton<ISyncConfiguration>(
                provider => provider.GetRequiredService<OpenDentalConfiguration>());
            
            // Register OpenDental API Service
            services.AddScoped<IPmsApiService, OpenDentalApiService>();
        }
        
        private static void RegisterDentrixServices(IServiceCollection services, IConfiguration configuration)
        {
            throw new NotImplementedException("EagleSoft integration coming soon");
        }

        private static void RegisterEagleSoftServices(IServiceCollection services, IConfiguration configuration)
        {
            throw new NotImplementedException("EagleSoft integration coming soon");
        }

        private static void CreateReportDirectory(IConfiguration config)
        {
            var reportsDirectory = config["ReportsDirectory"] ?? "reports";
            if(!Directory.Exists(reportsDirectory))
            {
                Directory.CreateDirectory(reportsDirectory);
                Log.Information($"Created Reports directory: {reportsDirectory}");
            }
            else
            {
                Log.Information($"Reports directory already exists: {reportsDirectory}");
            }
        }
    }
}