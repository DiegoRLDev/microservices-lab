using MicroservicesLab.PricingService.Infrastructure.Kafka;

namespace MicroservicesLab.PricingService.Tests;

public sealed class PriceUpdatedMessageParserTests
{
    [Fact]
    public void TryParse_accepts_valid_price_updated_event()
    {
        var payload = """
            {
              "symbol": "petr4",
              "price": 4893,
              "occurredAt": "2026-10-01T13:30:01.4110091Z"
            }
            """;

        var parsed = PriceUpdatedMessageParser.TryParse(payload, out var priceUpdated, out var error);

        Assert.True(parsed, error);
        Assert.Equal("PETR4", priceUpdated.Symbol);
        Assert.Equal(4893, priceUpdated.Price);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T13:30:01.4110091Z"), priceUpdated.OccurredAt);
    }

    [Theory]
    [InlineData("""{"symbol":"","price":4893,"occurredAt":"2026-10-01T13:30:01Z"}""")]
    [InlineData("""{"symbol":"PETR4","price":0,"occurredAt":"2026-10-01T13:30:01Z"}""")]
    [InlineData("""{"symbol":"PETR4","price":4893}""")]
    [InlineData("""not-json""")]
    public void TryParse_rejects_invalid_events(string payload)
    {
        var parsed = PriceUpdatedMessageParser.TryParse(payload, out _, out var error);

        Assert.False(parsed);
        Assert.NotEmpty(error);
    }
}
