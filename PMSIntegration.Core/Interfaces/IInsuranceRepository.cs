using PMSIntegration.Core.Entities;

namespace PMSIntegration.Core.Interfaces;

public interface IInsuranceRepository
{
    Task<int> BulkSaveAsync(List<Insurance> insurances);
}