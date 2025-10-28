namespace PMSIntegration.Core.Interfaces;

/// <summary>
/// Defines resilience policies for fault tolerance
/// </summary>
public interface IResiliencePolicy
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);
    Task ExecuteAsync(Func<Task> operation);
}