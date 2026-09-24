using MicroservicesLab.OrderService.Observability;

namespace MicroservicesLab.OrderService.Infrastructure.Resilience;

public sealed class InventoryCircuitBreaker(
    TimeProvider timeProvider,
    ILogger<InventoryCircuitBreaker> logger)
{
    private readonly object gate = new();
    private int consecutiveFailures;
    private DateTimeOffset? openedUntil;
    private bool halfOpenProbeInProgress;

    public bool CanExecute(out TimeSpan retryAfter)
    {
        lock (gate)
        {
            if (openedUntil is null)
            {
                retryAfter = TimeSpan.Zero;
                return true;
            }

            var now = timeProvider.GetUtcNow();

            if (now < openedUntil.Value)
            {
                retryAfter = openedUntil.Value - now;
                return false;
            }

            if (halfOpenProbeInProgress)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            halfOpenProbeInProgress = true;
            retryAfter = TimeSpan.Zero;
            logger.LogInformation("Inventory circuit breaker moved to half-open state");
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (gate)
        {
            var wasOpen = openedUntil is not null;

            consecutiveFailures = 0;
            openedUntil = null;
            halfOpenProbeInProgress = false;

            if (wasOpen)
            {
                logger.LogInformation("Inventory circuit breaker closed after a successful probe");
            }
        }
    }

    public void RecordFailure(InventoryResilienceOptions options, Exception exception)
    {
        lock (gate)
        {
            halfOpenProbeInProgress = false;
            consecutiveFailures++;

            if (openedUntil is not null || consecutiveFailures < options.CircuitBreakerFailureThreshold)
            {
                return;
            }

            openedUntil = timeProvider.GetUtcNow().AddSeconds(options.CircuitBreakerOpenSeconds);
            OrderTelemetry.CircuitBreakerOpens.Add(1);

            logger.LogWarning(
                exception,
                "Inventory circuit breaker opened for {OpenSeconds} seconds after {FailureCount} consecutive failed calls",
                options.CircuitBreakerOpenSeconds,
                consecutiveFailures);
        }
    }
}
