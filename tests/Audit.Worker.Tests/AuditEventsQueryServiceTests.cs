using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Domain;
using MicroservicesLab.AuditWorker.Infrastructure;
using MicroservicesLab.AuditWorker.Services;

namespace MicroservicesLab.AuditWorker.Tests;

public sealed class AuditEventsQueryServiceTests
{
    [Fact]
    public async Task ListAsync_returns_events_ordered_by_audited_at_descending()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
            CreateEvent("OrderCreated", "order-1", offset: 1, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateEvent("PaymentApproved", "order-1", offset: 2, auditedAt: DateTimeOffset.UtcNow));
        var service = new AuditEventsQueryService(dbContext);

        var result = await service.ListAsync(DefaultQuery(), CancellationToken.None);

        Assert.Equal(["PaymentApproved", "OrderCreated"], result.Select(item => item.EventType).ToArray());
    }

    [Fact]
    public async Task ListAsync_applies_limit_and_offset()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
            CreateEvent("OrderCreated", "order-1", offset: 1, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-3)),
            CreateEvent("PaymentApproved", "order-1", offset: 2, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateEvent("PaymentRejected", "order-2", offset: 3, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var service = new AuditEventsQueryService(dbContext);

        var result = await service.ListAsync(new AuditEventQuery(null, null, Limit: 1, Offset: 1), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("PaymentApproved", result[0].EventType);
    }

    [Fact]
    public async Task ListAsync_filters_by_event_type()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
            CreateEvent("OrderCreated", "order-1", offset: 1, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateEvent("PaymentApproved", "order-1", offset: 2, auditedAt: DateTimeOffset.UtcNow));
        var service = new AuditEventsQueryService(dbContext);

        var result = await service.ListAsync(new AuditEventQuery("PaymentApproved", null, Limit: 50, Offset: 0), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("PaymentApproved", result[0].EventType);
    }

    [Fact]
    public async Task ListAsync_filters_by_message_key()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
            CreateEvent("OrderCreated", "order-1", offset: 1, auditedAt: DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateEvent("OrderCreated", "order-2", offset: 2, auditedAt: DateTimeOffset.UtcNow));
        var service = new AuditEventsQueryService(dbContext);

        var result = await service.ListAsync(new AuditEventQuery(null, "order-2", Limit: 50, Offset: 0), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("order-2", result[0].MessageKey);
    }

    [Fact]
    public async Task ListAsync_returns_empty_list_when_no_events_match()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, CreateEvent("OrderCreated", "order-1", offset: 1, auditedAt: DateTimeOffset.UtcNow));
        var service = new AuditEventsQueryService(dbContext);

        var result = await service.ListAsync(new AuditEventQuery("PaymentRejected", null, Limit: 50, Offset: 0), CancellationToken.None);

        Assert.Empty(result);
    }

    private static AuditEventQuery DefaultQuery()
    {
        return new AuditEventQuery(null, null, Limit: 50, Offset: 0);
    }

    private static AuditEvent CreateEvent(
        string eventType,
        string messageKey,
        long offset,
        DateTimeOffset auditedAt)
    {
        return new AuditEvent(
            Guid.NewGuid(),
            eventType,
            eventType switch
            {
                "OrderCreated" => "order-created",
                "PaymentApproved" => "payment-approved",
                _ => "payment-rejected"
            },
            messageKey,
            partition: 0,
            offset,
            DateTimeOffset.UtcNow,
            "correlation-1",
            $$"""{"eventId":"{{Guid.NewGuid()}}","eventType":"{{eventType}}"}""",
            auditedAt);
    }

    private static async Task SeedAsync(AuditDbContext dbContext, params AuditEvent[] events)
    {
        dbContext.AuditEvents.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    private static AuditDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuditDbContext(options);
    }
}
