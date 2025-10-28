using System.Data.SQLite;
using System.IO.IsolatedStorage;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.Database.Repositories;

public class InsuranceRepository : IInsuranceRepository
{
    private readonly DatabaseContext _context;
    private readonly ILogger _logger;

    public InsuranceRepository(DatabaseContext context, ILogger<InsuranceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<int> BulkSaveAsync(List<Insurance> insurances)
    {
        const string sql = @"
            INSERT OR REPLACE INTO Insurance(
                PatientId, CarrierName, PolicyNumber,
                GroupNumber, PolicyholderName, Relationship,
                Priority
            ) VALUES (
                @patientId, @carrierName, @policyNumber,
                @groupNumber, @policyholderName, @relationship,
                @priority
            )";

        return await _context.ExecuteInTransactionAsync(async (transaction) =>
        {
            var count = 0;
            foreach (var insurance in insurances)
            {
                using var command = new SQLiteCommand(sql, _context.Connection, transaction);

                command.Parameters.AddWithValue("@patientId", insurance.PatientId);
                command.Parameters.AddWithValue("@carrierName", insurance.CarrierName);
                command.Parameters.AddWithValue("@policyNumber", insurance.PolicyNumber);
                command.Parameters.AddWithValue("@groupNumber", insurance.GroupNumber);
                command.Parameters.AddWithValue("@policyholderName", insurance.PolicyholderName);
                command.Parameters.AddWithValue("@relationship", insurance.Relationship);
                command.Parameters.AddWithValue("@priority", insurance.Priority);

                await command.ExecuteNonQueryAsync();
                count++;

                if (count % 10 == 0)
                {
                    _logger.LogInformation($"Progress: Saved {count}/{insurances.Count} insurances");
                }
            }

            return count;
        });
    }
}