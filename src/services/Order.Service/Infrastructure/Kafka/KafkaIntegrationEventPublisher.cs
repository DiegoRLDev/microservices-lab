using System.Text.Json;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MicroservicesLab.OrderService.Observability;
using MicroservicesLab.Shared.Contracts.Events;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace MicroservicesLab.OrderService.Infrastructure.Kafka;

public sealed class KafkaIntegrationEventPublisher(
    IOptions<KafkaOptions> options,
    ILogger<KafkaIntegrationEventPublisher> logger) : IIntegrationEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> producer = new ProducerBuilder<string, string>(
        new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();

    public async Task PublishAsync<TData>(
        string topic,
        string key,
        EventEnvelope<TData> envelope,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(envelope, SerializerOptions);
        using var activity = OrderTelemetry.KafkaActivitySource.StartActivity(
            $"Kafka produce {topic}",
            System.Diagnostics.ActivityKind.Producer);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.message.id", envelope.EventId.ToString());
        activity?.SetTag("messaging.kafka.message.key", key);
        activity?.SetTag("event.type", envelope.EventType);

        var headers = new Headers
        {
            new Header("event-id", Encoding.UTF8.GetBytes(envelope.EventId.ToString())),
            new Header("event-type", Encoding.UTF8.GetBytes(envelope.EventType)),
            new Header("correlation-id", Encoding.UTF8.GetBytes(envelope.CorrelationId))
        };

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(System.Diagnostics.Activity.Current?.Context ?? default, Baggage.Current),
            headers,
            static (carrier, name, value) => carrier.Add(name, Encoding.UTF8.GetBytes(value)));

        var result = await producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload,
                Headers = headers
            },
            cancellationToken);

        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);

        logger.LogInformation(
            "Published event {EventId} of type {EventType} to Kafka topic {Topic} partition {Partition} offset {Offset} traceId {TraceId}",
            envelope.EventId,
            envelope.EventType,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            System.Diagnostics.Activity.Current?.TraceId.ToString());
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
    }
}
