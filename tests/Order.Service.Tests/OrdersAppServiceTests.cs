using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroservicesLab.OrderService.Contracts;
using MicroservicesLab.OrderService.Domain;
using MicroservicesLab.OrderService.Infrastructure;
using MicroservicesLab.OrderService.Infrastructure.Grpc;
using MicroservicesLab.OrderService.Infrastructure.Kafka;
using MicroservicesLab.OrderService.Services;
using MicroservicesLab.Shared.Contracts.Events;

namespace MicroservicesLab.OrderService.Tests;

public sealed class OrdersAppServiceTests
{
    [Fact]
    public async Task CreateAsync_creates_pending_order()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new CapturingEventPublisher();
        var service = new OrdersAppService(
            dbContext,
            new AvailableInventoryClient(25.50m, 10),
            publisher,
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);
        var productId = Guid.NewGuid();

        var result = await service.CreateAsync(new CreateOrderRequest(productId, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal(productId, result.Value.ProductId);
        Assert.Equal(2, result.Value.Quantity);
        Assert.Equal(25.50m, result.Value.UnitPrice);
        Assert.Equal(51.00m, result.Value.TotalPrice);
        Assert.Equal(OrderStatus.Pending, result.Value.Status);
        Assert.Equal(1, await dbContext.Orders.CountAsync());
        Assert.Single(publisher.PublishedEvents);
        Assert.Equal(KafkaTopics.OrderCreated, publisher.PublishedEvents[0].Topic);
        Assert.Equal(result.Value.Id.ToString(), publisher.PublishedEvents[0].Key);
        Assert.Equal(IntegrationEventTypes.OrderCreated, publisher.PublishedEvents[0].EventType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_rejects_invalid_quantity(int quantity)
    {
        await using var dbContext = CreateDbContext();
        var service = new OrdersAppService(
            dbContext,
            new AvailableInventoryClient(10m, 10),
            new CapturingEventPublisher(),
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), quantity),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Quantity must be greater than zero.", result.Error);
        Assert.Empty(dbContext.Orders);
    }

    [Fact]
    public async Task CreateAsync_rejects_empty_product_id()
    {
        await using var dbContext = CreateDbContext();
        var service = new OrdersAppService(
            dbContext,
            new AvailableInventoryClient(10m, 10),
            new CapturingEventPublisher(),
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.Empty, 1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProductId must be provided.", result.Error);
        Assert.Empty(dbContext.Orders);
    }

    [Fact]
    public async Task GetByIdAsync_returns_existing_order()
    {
        await using var dbContext = CreateDbContext();
        var service = new OrdersAppService(
            dbContext,
            new AvailableInventoryClient(10m, 10),
            new CapturingEventPublisher(),
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);
        var created = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), 1),
            CancellationToken.None);

        var found = await service.GetByIdAsync(created.Value!.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(created.Value.Id, found.Id);
    }

    [Fact]
    public async Task CreateAsync_rejects_order_when_inventory_is_unavailable()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new CapturingEventPublisher();
        var service = new OrdersAppService(
            dbContext,
            new UnavailableInventoryClient(1),
            publisher,
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), 2),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient stock.", result.Error);
        Assert.Empty(dbContext.Orders);
        Assert.Empty(publisher.PublishedEvents);
    }

    [Fact]
    public async Task CreateAsync_marks_inventory_client_failure_as_dependency_unavailable()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new CapturingEventPublisher();
        var service = new OrdersAppService(
            dbContext,
            new FailingInventoryClient(),
            publisher,
            CreateKafkaOptions(),
            NullLogger<OrdersAppService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), 1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Inventory service is unavailable.", result.Error);
        Assert.Equal(OperationErrorType.DependencyUnavailable, result.ErrorType);
        Assert.Empty(dbContext.Orders);
        Assert.Empty(publisher.PublishedEvents);
    }

    [Fact]
    public async Task ApplyPaymentApprovedAsync_marks_order_as_paid()
    {
        await using var dbContext = CreateDbContext();
        var order = new Order(Guid.NewGuid(), 1, 25m, DateTimeOffset.UtcNow);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new PaymentEventsAppService(
            dbContext,
            NullLogger<PaymentEventsAppService>.Instance);

        await service.ApplyPaymentApprovedAsync(
            new EventEnvelope<PaymentApprovedData>(
                Guid.NewGuid(),
                IntegrationEventTypes.PaymentApproved,
                DateTimeOffset.UtcNow,
                Guid.NewGuid().ToString(),
                new PaymentApprovedData(
                    Guid.NewGuid(),
                    order.Id,
                    order.TotalPrice,
                    "Approved",
                    "amount within automatic approval limit",
                    DateTimeOffset.UtcNow)),
            CancellationToken.None);

        var updated = await dbContext.Orders.SingleAsync(entity => entity.Id == order.Id);
        Assert.Equal(OrderStatus.Paid, updated.Status);
    }

    [Fact]
    public async Task ApplyPaymentRejectedAsync_marks_order_as_payment_rejected()
    {
        await using var dbContext = CreateDbContext();
        var order = new Order(Guid.NewGuid(), 1, 25000m, DateTimeOffset.UtcNow);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new PaymentEventsAppService(
            dbContext,
            NullLogger<PaymentEventsAppService>.Instance);

        await service.ApplyPaymentRejectedAsync(
            new EventEnvelope<PaymentRejectedData>(
                Guid.NewGuid(),
                IntegrationEventTypes.PaymentRejected,
                DateTimeOffset.UtcNow,
                Guid.NewGuid().ToString(),
                new PaymentRejectedData(
                    Guid.NewGuid(),
                    order.Id,
                    order.TotalPrice,
                    "Rejected",
                    "amount exceeds automatic approval limit",
                    DateTimeOffset.UtcNow)),
            CancellationToken.None);

        var updated = await dbContext.Orders.SingleAsync(entity => entity.Id == order.Id);
        Assert.Equal(OrderStatus.PaymentRejected, updated.Status);
    }

    private static OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OrdersDbContext(options);
    }

    private static IOptions<KafkaOptions> CreateKafkaOptions()
    {
        return Options.Create(new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            OrderCreatedTopic = KafkaTopics.OrderCreated
        });
    }

    private sealed class AvailableInventoryClient(decimal unitPrice, int availableQuantity) : IInventoryClient
    {
        public Task<InventoryAvailability> CheckAvailabilityAsync(
            Guid productId,
            int quantity,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new InventoryAvailability(
                productId,
                true,
                quantity,
                availableQuantity,
                unitPrice));
        }
    }

    private sealed class UnavailableInventoryClient(int availableQuantity) : IInventoryClient
    {
        public Task<InventoryAvailability> CheckAvailabilityAsync(
            Guid productId,
            int quantity,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new InventoryAvailability(
                productId,
                false,
                quantity,
                availableQuantity,
                0));
        }
    }

    private sealed class FailingInventoryClient : IInventoryClient
    {
        public Task<InventoryAvailability> CheckAvailabilityAsync(
            Guid productId,
            int quantity,
            CancellationToken cancellationToken)
        {
            throw new InventoryClientException(
                "Inventory service is unavailable.",
                new InvalidOperationException("Simulated dependency failure."));
        }
    }

    private sealed class CapturingEventPublisher : IIntegrationEventPublisher
    {
        public List<PublishedEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<TData>(
            string topic,
            string key,
            EventEnvelope<TData> envelope,
            CancellationToken cancellationToken)
        {
            PublishedEvents.Add(new PublishedEvent(topic, key, envelope.EventType, envelope.EventId));

            return Task.CompletedTask;
        }
    }

    private sealed record PublishedEvent(string Topic, string Key, string EventType, Guid EventId);
}
