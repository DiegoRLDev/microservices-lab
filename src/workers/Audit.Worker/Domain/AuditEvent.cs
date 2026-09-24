namespace MicroservicesLab.AuditWorker.Domain;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid eventId,
        string eventType,
        string topic,
        string messageKey,
        int partition,
        long offset,
        DateTimeOffset? occurredAt,
        string correlationId,
        string payload,
        DateTimeOffset auditedAt)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("EventId must be provided.", nameof(eventId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("EventType must be provided.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Topic must be provided.", nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload must be provided.", nameof(payload));
        }

        Id = Guid.NewGuid();
        EventId = eventId;
        EventType = eventType;
        Topic = topic;
        MessageKey = messageKey;
        Partition = partition;
        Offset = offset;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        Payload = payload;
        AuditedAt = auditedAt;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Topic { get; private set; } = string.Empty;

    public string MessageKey { get; private set; } = string.Empty;

    public int Partition { get; private set; }

    public long Offset { get; private set; }

    public DateTimeOffset? OccurredAt { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset AuditedAt { get; private set; }
}
