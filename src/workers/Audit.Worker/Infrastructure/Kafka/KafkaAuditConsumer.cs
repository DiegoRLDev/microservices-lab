using System.Text.Json;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MicroservicesLab.AuditWorker.Observability;
using MicroservicesLab.AuditWorker.Services;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace MicroservicesLab.AuditWorker.Infrastructure.Kafka;

public sealed class KafkaAuditConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaAuditConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaOptions = options.Value;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        var topics = kafkaOptions.Topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (topics.Length == 0)
        {
            throw new InvalidOperationException("At least one Kafka topic must be configured for Audit.Worker.");
        }

        logger.LogInformation(
            "Audit.Worker configuring Kafka subscription. BootstrapServers: {BootstrapServers}. ConsumerGroup: {ConsumerGroup}. Topics: {Topics}",
            kafkaOptions.BootstrapServers,
            kafkaOptions.ConsumerGroup,
            string.Join(", ", topics));

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topics);

        logger.LogInformation(
            "Audit.Worker subscribed to Kafka topics {Topics} with consumer group {ConsumerGroup}",
            string.Join(", ", topics),
            kafkaOptions.ConsumerGroup);

        await Task.Yield();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;

                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await HandleAsync(result, stoppingToken);
                consumer.Commit(result);
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        var parentContext = Propagators.DefaultTextMapPropagator.Extract(
            default,
            result.Message.Headers,
            static (headers, name) =>
            {
                var values = headers
                    .Where(header => header.Key == name)
                    .Select(header => Encoding.UTF8.GetString(header.GetValueBytes()))
                    .ToArray();

                return values;
            });

        Baggage.Current = parentContext.Baggage;

        using var activity = AuditTelemetry.KafkaActivitySource.StartActivity(
            $"Kafka audit {result.Topic}",
            System.Diagnostics.ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.operation", "consume");
        activity?.SetTag("messaging.kafka.message.key", result.Message.Key);
        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);

        var message = ParseMessage(result);

        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<AuditEventProcessor>();

        await processor.RecordAsync(message, cancellationToken);
    }

    private static AuditEventMessage ParseMessage(ConsumeResult<string, string> result)
    {
        using var document = JsonDocument.Parse(result.Message.Value);
        var root = document.RootElement;

        var eventId = root.GetProperty("eventId").GetGuid();
        var eventType = root.GetProperty("eventType").GetString()
            ?? throw new InvalidOperationException("eventType is required.");
        var correlationId = root.GetProperty("correlationId").GetString() ?? string.Empty;
        var occurredAt = root.TryGetProperty("occurredAt", out var occurredAtElement)
            ? occurredAtElement.GetDateTimeOffset()
            : (DateTimeOffset?)null;

        return new AuditEventMessage(
            eventId,
            eventType,
            result.Topic,
            result.Message.Key ?? string.Empty,
            result.Partition.Value,
            result.Offset.Value,
            occurredAt,
            correlationId,
            result.Message.Value);
    }
}
