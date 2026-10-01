using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MicroservicesLab.PricingService.Services;

namespace MicroservicesLab.PricingService.Infrastructure.Kafka;

public sealed class KafkaPriceUpdatedConsumer(
    IOptions<KafkaOptions> options,
    LatestPriceStore store,
    ILogger<KafkaPriceUpdatedConsumer> logger) : BackgroundService
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

        if (string.IsNullOrWhiteSpace(kafkaOptions.Topic))
        {
            throw new InvalidOperationException("Kafka topic must be configured for Pricing.Service.");
        }

        logger.LogInformation(
            "Pricing.Service configuring Kafka subscription. BootstrapServers: {BootstrapServers}. ConsumerGroup: {ConsumerGroup}. Topic: {Topic}",
            kafkaOptions.BootstrapServers,
            kafkaOptions.ConsumerGroup,
            kafkaOptions.Topic);

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(kafkaOptions.Topic);

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

                Handle(result);
                consumer.Commit(result);
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private void Handle(ConsumeResult<string, string> result)
    {
        if (!PriceUpdatedMessageParser.TryParse(
                result.Message.Value,
                out var priceUpdated,
                out var error))
        {
            logger.LogWarning(
                "Ignoring invalid PriceUpdated message from topic {Topic} partition {Partition} offset {Offset}. Reason: {Reason}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                error);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Message.Key)
            && !string.Equals(result.Message.Key, priceUpdated.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "PriceUpdated message key {MessageKey} does not match symbol {Symbol} at partition {Partition} offset {Offset}",
                result.Message.Key,
                priceUpdated.Symbol,
                result.Partition.Value,
                result.Offset.Value);
        }

        store.Update(priceUpdated);

        logger.LogDebug(
            "Updated latest price for {Symbol} from Kafka key {MessageKey} partition {Partition} offset {Offset}",
            priceUpdated.Symbol,
            result.Message.Key,
            result.Partition.Value,
            result.Offset.Value);
    }
}
