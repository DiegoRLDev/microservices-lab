using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroservicesLab.AuditWorker.Infrastructure;
using MicroservicesLab.AuditWorker.Services;

namespace MicroservicesLab.AuditWorker.Tests;

public sealed class AuditEventProcessorTests
{
    [Fact]
    public async Task RecordAsync_records_audit_event()
    {
        await using var dbContext = CreateDbContext();
        var processor = new AuditEventProcessor(
            dbContext,
            NullLogger<AuditEventProcessor>.Instance);
        var eventId = Guid.NewGuid();

        var recorded = await processor.RecordAsync(
            new AuditEventMessage(
                eventId,
                "OrderCreated",
                "order-created",
                "order-1",
                Partition: 1,
                Offset: 10,
                DateTimeOffset.UtcNow,
                "correlation-1",
                """{"eventId":"00000000-0000-0000-0000-000000000001","eventType":"OrderCreated"}"""),
            CancellationToken.None);

        Assert.True(recorded);

        var auditEvent = await dbContext.AuditEvents.SingleAsync();
        Assert.Equal(eventId, auditEvent.EventId);
        Assert.Equal("OrderCreated", auditEvent.EventType);
        Assert.Equal("order-created", auditEvent.Topic);
        Assert.Equal("order-1", auditEvent.MessageKey);
        Assert.Equal(1, auditEvent.Partition);
        Assert.Equal(10, auditEvent.Offset);
    }

    [Fact]
    public async Task RecordAsync_ignores_duplicate_event_id()
    {
        await using var dbContext = CreateDbContext();
        var processor = new AuditEventProcessor(
            dbContext,
            NullLogger<AuditEventProcessor>.Instance);
        var eventId = Guid.NewGuid();
        var message = new AuditEventMessage(
            eventId,
            "PaymentApproved",
            "payment-approved",
            "order-1",
            Partition: 0,
            Offset: 2,
            DateTimeOffset.UtcNow,
            "correlation-1",
            """{"eventId":"00000000-0000-0000-0000-000000000001","eventType":"PaymentApproved"}""");

        var first = await processor.RecordAsync(message, CancellationToken.None);
        var second = await processor.RecordAsync(message, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await dbContext.AuditEvents.CountAsync());
    }

    private static AuditDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuditDbContext(options);
    }
}
