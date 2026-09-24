namespace MicroservicesLab.Shared.Contracts.Events;

public static class IntegrationEventTypes
{
    public const string OrderCreated = "OrderCreated";
    public const string PaymentApproved = "PaymentApproved";
    public const string PaymentRejected = "PaymentRejected";
}

public sealed record OrderCreatedData(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTimeOffset CreatedAt);
