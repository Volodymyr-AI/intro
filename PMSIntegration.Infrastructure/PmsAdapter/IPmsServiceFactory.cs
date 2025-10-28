using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.PmsAdapter;

public interface IPmsServiceFactory
{
    IPmsApiService CreatePmsService(PmsProvider provider);
}