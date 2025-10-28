using PMSIntegration.Core.Entities;

namespace PMSIntegration.Core.Interfaces;

public interface IPatientRepository
{
    Task<HashSet<int>> GetAllIdsAsync();
    Task<List<Patient>> GetUnsyncedAsync(int limit = 100);
    Task<int> BulkSaveAsync(List<Patient> patients);
    Task<bool> MarkAsSyncedAsync(List<int> patientIds);
    Task<SyncState?> GetSyncStateAsync();
    Task UpdateSyncStateAsync(SyncState state);
}