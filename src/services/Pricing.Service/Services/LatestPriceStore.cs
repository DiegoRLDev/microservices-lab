using System.Collections.Concurrent;
using MicroservicesLab.PricingService.Contracts;

namespace MicroservicesLab.PricingService.Services;

public sealed class LatestPriceStore
{
    private readonly ConcurrentDictionary<string, PriceResponse> prices = new(StringComparer.OrdinalIgnoreCase);

    public PriceResponse Update(PriceUpdatedEvent priceUpdated)
    {
        var latest = new PriceResponse(
            Normalize(priceUpdated.Symbol),
            priceUpdated.Price,
            priceUpdated.OccurredAt);

        prices.AddOrUpdate(latest.Symbol, latest, (_, _) => latest);

        return latest;
    }

    public PriceResponse? Get(string symbol)
    {
        return prices.TryGetValue(Normalize(symbol), out var price)
            ? price
            : null;
    }

    public IReadOnlyCollection<PriceResponse> GetAll()
    {
        return prices.Values
            .OrderBy(price => price.Symbol, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }
}
