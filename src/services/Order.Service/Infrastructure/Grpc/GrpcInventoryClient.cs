using Grpc.Core;
using Microsoft.Extensions.Options;
using MicroservicesLab.OrderService.Infrastructure.Resilience;
using MicroservicesLab.OrderService.Observability;
using System.Globalization;
using MicroservicesLab.Protos.Inventory;

namespace MicroservicesLab.OrderService.Infrastructure.Grpc;

public sealed class GrpcInventoryClient(
    Inventory.InventoryClient client,
    IOptions<InventoryResilienceOptions> resilienceOptions,
    InventoryCircuitBreaker circuitBreaker,
    ILogger<GrpcInventoryClient> logger) : IInventoryClient
{
    public async Task<InventoryAvailability> CheckAvailabilityAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var options = resilienceOptions.Value;
        OrderTelemetry.RecordInventoryCall("started");

        if (!circuitBreaker.CanExecute(out var retryAfter))
        {
            OrderTelemetry.RecordInventoryFailure("circuit_open");
            logger.LogWarning(
                "Inventory circuit breaker is open. Rejecting product {ProductId} availability check for another {RetryAfterSeconds:n1} seconds",
                productId,
                retryAfter.TotalSeconds);

            throw new InventoryClientException("Inventory service is temporarily unavailable.", new TimeoutException("Inventory circuit breaker is open."));
        }

        var maxAttempts = Math.Max(1, options.RetryAttempts + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

                logger.LogInformation(
                    "Checking inventory availability for product {ProductId}. Attempt {Attempt}/{MaxAttempts}",
                    productId,
                    attempt,
                    maxAttempts);

                var response = await client.CheckAvailabilityAsync(
                    new CheckAvailabilityRequest
                    {
                        ProductId = productId.ToString(),
                        Quantity = quantity
                    },
                    cancellationToken: timeout.Token);

                circuitBreaker.RecordSuccess();

                return new InventoryAvailability(
                    Guid.Parse(response.ProductId),
                    response.IsAvailable,
                    response.RequestedQuantity,
                    response.AvailableQuantity,
                    decimal.Parse(response.UnitPrice, CultureInfo.InvariantCulture));
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
            {
                circuitBreaker.RecordSuccess();

                logger.LogWarning(
                    exception,
                    "Inventory product {ProductId} was not found while creating an order",
                    productId);

                return new InventoryAvailability(productId, false, quantity, 0, 0);
            }
            catch (RpcException exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Inventory availability check failed for product {ProductId}. Retrying attempt {NextAttempt}/{MaxAttempts}",
                    productId,
                    attempt + 1,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds), cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Inventory availability check timed out for product {ProductId}. Retrying attempt {NextAttempt}/{MaxAttempts}",
                    productId,
                    attempt + 1,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds), cancellationToken);
            }
            catch (Exception exception) when (IsTransientInventoryFailure(exception))
            {
                OrderTelemetry.RecordInventoryFailure("transient_failure");
                circuitBreaker.RecordFailure(options, exception);

                logger.LogError(
                    exception,
                    "Failed to check inventory availability for product {ProductId} after {Attempts} attempts",
                    productId,
                    maxAttempts);

                throw new InventoryClientException("Inventory service is unavailable.", exception);
            }
        }

        throw new InventoryClientException(
            "Inventory service is unavailable.",
            new InvalidOperationException("Inventory availability check failed without an exception."));
    }

    private static bool IsTransientInventoryFailure(Exception exception)
    {
        return exception is RpcException or OperationCanceledException;
    }
}
