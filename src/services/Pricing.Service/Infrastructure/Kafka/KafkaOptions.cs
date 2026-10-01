namespace MicroservicesLab.PricingService.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public bool Enabled { get; init; } = true;

    public string BootstrapServers { get; init; } = "localhost:9092";

    public string Topic { get; init; } = "price-updated";

    public string ConsumerGroup { get; init; } = "pricing-service";
}
