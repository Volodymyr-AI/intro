using PMSIntegration.Core.Entities;

namespace PMSIntegration.Core.Interfaces;

public interface IPmsApiService
{
    Task<bool> IsAvailableAsync();
    Task<List<Patient>> GetPatientsAsync(DateTime? since = null);
    Task<List<Insurance>> GetPatientInsuranceAsync(int patientId);
}