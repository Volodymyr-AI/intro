using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Enums;

namespace PMSIntegration.Core.Interfaces;

public interface IReportRepository
{
    Task<int> InsertAsync(Report report);
    Task<Report?> GetByIdAsync(int id);
    Task<List<Report>> GetByStatusAsync(ReportStatus status);
    Task<bool> UpdateAsync(Report report);
}