namespace MicroservicesLab.OrderService.Infrastructure.Resilience;

public sealed class InventoryResilienceOptions
{
    public int TimeoutSeconds { get; init; } = 2;

    public int RetryAttempts { get; init; } = 2;

    public int RetryDelayMilliseconds { get; init; } = 200;

    public int CircuitBreakerFailureThreshold { get; init; } = 3;

    public int CircuitBreakerOpenSeconds { get; init; } = 10;
}
