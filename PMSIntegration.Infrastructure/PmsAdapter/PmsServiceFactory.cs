using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Interfaces;
using PMSIntegration.Infrastructure.PmsAdapter.OpenDental;

namespace PMSIntegration.Infrastructure.PmsAdapter;

public class PmsServiceFactory : IPmsServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PmsServiceFactory> _logger;

    public PmsServiceFactory(IServiceProvider serviceProvider, ILogger<PmsServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IPmsApiService CreatePmsService(PmsProvider provider)
    {
        _logger.LogInformation($"Creating PMS service for provider: {provider}");

        return provider switch
        {
            PmsProvider.OpenDental => _serviceProvider.GetRequiredService<OpenDentalApiService>(),
            // Future implementations:
            // PmsProvider.Dentrix => _serviceProvider.GetRequiredService<DentrixApiService>(),
            // PmsProvider.EagleSoft => _serviceProvider.GetRequiredService<EagleSoftApiService>(),
            _ => throw new NotSupportedException($"PMS provider {provider} is not supported yet")
        };
    }
}