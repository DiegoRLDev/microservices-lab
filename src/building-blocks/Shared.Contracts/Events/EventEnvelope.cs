namespace MicroservicesLab.Shared.Contracts.Events;

public sealed record EventEnvelope<TData>(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    TData Data);

