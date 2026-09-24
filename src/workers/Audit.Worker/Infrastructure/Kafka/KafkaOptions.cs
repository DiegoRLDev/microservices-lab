namespace MicroservicesLab.AuditWorker.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";

    public string ConsumerGroup { get; init; } = "audit-worker";

    public string[] Topics { get; init; } =
    [
        "order-created",
        "payment-approved",
        "payment-rejected"
    ];
}
