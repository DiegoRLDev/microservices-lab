using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MicroservicesLab.OrderService.Contracts;
using MicroservicesLab.OrderService.Domain;
using MicroservicesLab.OrderService.Infrastructure.Grpc;
using MicroservicesLab.OrderService.Infrastructure;
using MicroservicesLab.OrderService.Infrastructure.Kafka;
using MicroservicesLab.OrderService.Observability;
using MicroservicesLab.Shared.Contracts.Events;
using System.Diagnostics;

namespace MicroservicesLab.OrderService.Services;

public sealed class OrdersAppService(
    OrdersDbContext dbContext,
    IInventoryClient inventoryClient,
    IIntegrationEventPublisher eventPublisher,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<OrdersAppService> logger)
{
    public async Task<OperationResult<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            OrderTelemetry.RecordOrderRejected("invalid_product_id");
            return OperationResult<OrderResponse>.Failure("ProductId must be provided.");
        }

        if (request.Quantity <= 0)
        {
            OrderTelemetry.RecordOrderRejected("invalid_quantity");
            return OperationResult<OrderResponse>.Failure("Quantity must be greater than zero.");
        }

        InventoryAvailability availability;

        try
        {
            availability = await inventoryClient.CheckAvailabilityAsync(
                request.ProductId,
                request.Quantity,
                cancellationToken);
        }
        catch (InventoryClientException exception)
        {
            logger.LogWarning(
                exception,
                "Failed to create order because Inventory.Service is unavailable for product {ProductId}",
                request.ProductId);

            OrderTelemetry.RecordOrderRejected("inventory_unavailable");

            return OperationResult<OrderResponse>.Failure(
                "Inventory service is unavailable.",
                OperationErrorType.DependencyUnavailable);
        }

        if (!availability.IsAvailable)
        {
            logger.LogInformation(
                "Rejected order creation for product {ProductId}. Requested {RequestedQuantity}, available {AvailableQuantity}",
                request.ProductId,
                request.Quantity,
                availability.AvailableQuantity);

            OrderTelemetry.RecordOrderRejected("insufficient_stock");
            return OperationResult<OrderResponse>.Failure("Insufficient stock.");
        }

        var order = new Order(
            request.ProductId,
            request.Quantity,
            availability.UnitPrice,
            createdAt: DateTimeOffset.UtcNow);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var orderCreated = new EventEnvelope<OrderCreatedData>(
            Guid.NewGuid(),
            IntegrationEventTypes.OrderCreated,
            DateTimeOffset.UtcNow,
            GetCorrelationId(),
            new OrderCreatedData(
                order.Id,
                order.ProductId,
                order.Quantity,
                order.UnitPrice,
                order.TotalPrice,
                order.Status.ToString(),
                order.CreatedAt));

        await eventPublisher.PublishAsync(
            kafkaOptions.Value.OrderCreatedTopic,
            order.Id.ToString(),
            orderCreated,
            cancellationToken);

        logger.LogInformation(
            "Created order {OrderId} for product {ProductId} with status {OrderStatus} and published event {EventId}",
            order.Id,
            order.ProductId,
            order.Status,
            orderCreated.EventId);

        OrderTelemetry.OrdersCreated.Add(1);

        return OperationResult<OrderResponse>.Success(order.ToResponse());
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == id)
            .Select(order => order.ToResponse())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => order.ToResponse())
            .ToListAsync(cancellationToken);
    }

    private static string GetCorrelationId()
    {
        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
    }
}

internal static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order)
    {
        return new OrderResponse(
            order.Id,
            order.ProductId,
            order.Quantity,
            order.UnitPrice,
            order.TotalPrice,
            order.Status,
            order.CreatedAt);
    }
}
