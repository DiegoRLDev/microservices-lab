namespace MicroservicesLab.AuditWorker.Services;

public sealed record AuditEventMessage(
    Guid EventId,
    string EventType,
    string Topic,
    string MessageKey,
    int Partition,
    long Offset,
    DateTimeOffset? OccurredAt,
    string CorrelationId,
    string Payload);
