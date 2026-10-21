using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Contracts;
using MicroservicesLab.AuditWorker.Domain;
using MicroservicesLab.AuditWorker.Infrastructure;

namespace MicroservicesLab.AuditWorker.Services;

public sealed class AuditEventsQueryService(AuditDbContext dbContext)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public async Task<IReadOnlyList<AuditEventResponse>> ListAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken)
    {
        var events = dbContext.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            events = events.Where(auditEvent => auditEvent.EventType == query.EventType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.MessageKey))
        {
            events = events.Where(auditEvent => auditEvent.MessageKey == query.MessageKey.Trim());
        }

        return await events
            .OrderByDescending(auditEvent => auditEvent.AuditedAt)
            .ThenByDescending(auditEvent => auditEvent.Offset)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(auditEvent => auditEvent.ToResponse())
            .ToListAsync(cancellationToken);
    }
}

internal static class AuditEventMappings
{
    public static AuditEventResponse ToResponse(this AuditEvent auditEvent)
    {
        return new AuditEventResponse(
            auditEvent.Id,
            auditEvent.EventId,
            auditEvent.EventType,
            auditEvent.Topic,
            auditEvent.MessageKey,
            auditEvent.Partition,
            auditEvent.Offset,
            auditEvent.OccurredAt,
            auditEvent.CorrelationId,
            auditEvent.Payload,
            auditEvent.AuditedAt);
    }
}
