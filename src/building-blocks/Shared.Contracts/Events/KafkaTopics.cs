namespace MicroservicesLab.Shared.Contracts.Events;

public static class KafkaTopics
{
    public const string OrderCreated = "order-created";
    public const string PaymentApproved = "payment-approved";
    public const string PaymentRejected = "payment-rejected";
    public const string AuditEvents = "audit-events";
}

