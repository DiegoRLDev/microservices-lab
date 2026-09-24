using System.Text.Json;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MicroservicesLab.OrderService.Observability;
using MicroservicesLab.OrderService.Services;
using MicroservicesLab.Shared.Contracts.Events;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace MicroservicesLab.OrderService.Infrastructure.Kafka;

public sealed class PaymentEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<PaymentEventsConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaOptions = options.Value;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.PaymentEventsConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

        consumer.Subscribe([
            kafkaOptions.PaymentApprovedTopic,
            kafkaOptions.PaymentRejectedTopic
        ]);

        logger.LogInformation(
            "Order.Service subscribed to payment topics {PaymentApprovedTopic} and {PaymentRejectedTopic} with consumer group {ConsumerGroup}",
            kafkaOptions.PaymentApprovedTopic,
            kafkaOptions.PaymentRejectedTopic,
            kafkaOptions.PaymentEventsConsumerGroup);

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

        using var activity = OrderTelemetry.KafkaActivitySource.StartActivity(
            $"Kafka consume {result.Topic}",
            System.Diagnostics.ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.operation", "consume");
        activity?.SetTag("messaging.kafka.message.key", result.Message.Key);
        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);

        using var scope = scopeFactory.CreateScope();
        var appService = scope.ServiceProvider.GetRequiredService<PaymentEventsAppService>();

        if (result.Topic == options.Value.PaymentApprovedTopic)
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope<PaymentApprovedData>>(result.Message.Value, JsonOptions)
                ?? throw new InvalidOperationException("PaymentApproved event payload is empty.");

            ValidatePaymentEvent(result, envelope.EventType, envelope.Data.OrderId);
            OrderTelemetry.RecordPaymentEventConsumed(envelope.EventType);
            await appService.ApplyPaymentApprovedAsync(envelope, cancellationToken);
            return;
        }

        if (result.Topic == options.Value.PaymentRejectedTopic)
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope<PaymentRejectedData>>(result.Message.Value, JsonOptions)
                ?? throw new InvalidOperationException("PaymentRejected event payload is empty.");

            ValidatePaymentEvent(result, envelope.EventType, envelope.Data.OrderId);
            OrderTelemetry.RecordPaymentEventConsumed(envelope.EventType);
            await appService.ApplyPaymentRejectedAsync(envelope, cancellationToken);
            return;
        }

        logger.LogWarning("Ignoring unexpected Kafka topic {Topic}", result.Topic);
    }

    private void ValidatePaymentEvent(
        ConsumeResult<string, string> result,
        string eventType,
        Guid orderId)
    {
        var expectedEventType = result.Topic == options.Value.PaymentApprovedTopic
            ? IntegrationEventTypes.PaymentApproved
            : IntegrationEventTypes.PaymentRejected;

        if (eventType != expectedEventType)
        {
            throw new InvalidOperationException(
                $"Unexpected event type {eventType} in topic {result.Topic}.");
        }

        if (result.Message.Key != orderId.ToString())
        {
            logger.LogWarning(
                "Payment event key {KafkaKey} differs from OrderId {OrderId}",
                result.Message.Key,
                orderId);
        }

        logger.LogInformation(
            "Consumed payment event {EventType} for order {OrderId} from topic {Topic} partition {Partition} offset {Offset}",
            eventType,
            orderId,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value);
    }
}
