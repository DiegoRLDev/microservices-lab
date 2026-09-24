using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Domain;
using MicroservicesLab.AuditWorker.Infrastructure;

namespace MicroservicesLab.AuditWorker.Services;

public sealed class AuditEventProcessor(
    AuditDbContext dbContext,
    ILogger<AuditEventProcessor> logger)
{
    public async Task<bool> RecordAsync(
        AuditEventMessage message,
        CancellationToken cancellationToken)
    {
        var alreadyRecorded = await dbContext.AuditEvents
            .AnyAsync(auditEvent => auditEvent.EventId == message.EventId, cancellationToken);

        if (alreadyRecorded)
        {
            logger.LogInformation(
                "Audit event {EventId} was already recorded",
                message.EventId);

            return false;
        }

        var auditEvent = new AuditEvent(
            message.EventId,
            message.EventType,
            message.Topic,
            message.MessageKey,
            message.Partition,
            message.Offset,
            message.OccurredAt,
            message.CorrelationId,
            message.Payload,
            DateTimeOffset.UtcNow);

        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Recorded audit event {EventId} of type {EventType} from topic {Topic} partition {Partition} offset {Offset}",
            message.EventId,
            message.EventType,
            message.Topic,
            message.Partition,
            message.Offset);

        return true;
    }
}
