namespace MicroservicesLab.OrderService.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";

    public string OrderCreatedTopic { get; init; } = "order-created";

    public string PaymentApprovedTopic { get; init; } = "payment-approved";

    public string PaymentRejectedTopic { get; init; } = "payment-rejected";

    public string PaymentEventsConsumerGroup { get; init; } = "order-service-payment-events";
}
