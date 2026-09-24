namespace MicroservicesLab.Shared.Contracts.Events;

public sealed record PaymentApprovedData(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string Reason,
    DateTimeOffset ProcessedAt);

public sealed record PaymentRejectedData(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Status,
    string Reason,
    DateTimeOffset ProcessedAt);
