namespace MicroservicesLab.AuditWorker.Services;

public sealed record AuditEventQuery(
    string? EventType,
    string? MessageKey,
    int Limit,
    int Offset);
