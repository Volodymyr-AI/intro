namespace PMSIntegration.Core.Interfaces;

public interface IHeartbeatService
{
    Task<bool> CheckHealthAsync();
    Task ReportHeartbeatAsync();
}