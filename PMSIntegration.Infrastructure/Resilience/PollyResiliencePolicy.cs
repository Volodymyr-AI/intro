using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.Resilience;

/// <summary>
/// Implements resilience using Polly library
/// </summary>
public class PollyResiliencePolicy : IResiliencePolicy
{
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<PollyResiliencePolicy> _logger;

    public PollyResiliencePolicy(ILogger<PollyResiliencePolicy> logger)
    {
        _logger = logger;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning($"Retry {retryCount} after {timespan}s delay");
                });
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        return await _retryPolicy.ExecuteAsync(operation);
    }
    
    public async Task ExecuteAsync(Func<Task> operation)
    {
        await _retryPolicy.ExecuteAsync(operation);
    }
}