using System.Text.Json;
using MicroservicesLab.PricingService.Contracts;

namespace MicroservicesLab.PricingService.Infrastructure.Kafka;

public static class PriceUpdatedMessageParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(
        string? payload,
        out PriceUpdatedEvent priceUpdated,
        out string error)
    {
        priceUpdated = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Kafka message payload is empty.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PriceUpdatedEvent>(payload, SerializerOptions);

            if (parsed is null)
            {
                error = "Kafka message payload is not a valid PriceUpdated event.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsed.Symbol))
            {
                error = "PriceUpdated symbol is required.";
                return false;
            }

            if (parsed.Price <= 0)
            {
                error = "PriceUpdated price must be greater than zero.";
                return false;
            }

            if (parsed.OccurredAt == default)
            {
                error = "PriceUpdated occurredAt is required.";
                return false;
            }

            priceUpdated = parsed with
            {
                Symbol = parsed.Symbol.Trim().ToUpperInvariant()
            };
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Kafka message payload is invalid JSON: {ex.Message}";
            return false;
        }
    }
}
