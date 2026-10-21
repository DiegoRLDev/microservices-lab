namespace MicroservicesLab.AuditWorker.Contracts;

public sealed record AuditEventResponse(
    Guid Id,
    Guid EventId,
    string EventType,
    string Topic,
    string MessageKey,
    int Partition,
    long Offset,
    DateTimeOffset? OccurredAt,
    string CorrelationId,
    string Payload,
    DateTimeOffset AuditedAt);
