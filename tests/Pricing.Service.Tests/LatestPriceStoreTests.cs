using MicroservicesLab.PricingService.Contracts;
using MicroservicesLab.PricingService.Services;

namespace MicroservicesLab.PricingService.Tests;

public sealed class LatestPriceStoreTests
{
    [Fact]
    public void Update_stores_latest_price_by_symbol()
    {
        var store = new LatestPriceStore();
        var first = new PriceUpdatedEvent("PETR4", 3810, DateTimeOffset.UtcNow.AddSeconds(-1));
        var second = new PriceUpdatedEvent("PETR4", 3815, DateTimeOffset.UtcNow);

        store.Update(first);
        store.Update(second);

        var latest = store.Get("petr4");

        Assert.NotNull(latest);
        Assert.Equal("PETR4", latest.Symbol);
        Assert.Equal(3815, latest.Price);
        Assert.Equal(second.OccurredAt, latest.OccurredAt);
    }

    [Fact]
    public void GetAll_returns_multiple_symbols_ordered_by_symbol()
    {
        var store = new LatestPriceStore();

        store.Update(new PriceUpdatedEvent("VALE3", 7420, DateTimeOffset.UtcNow));
        store.Update(new PriceUpdatedEvent("PETR4", 3810, DateTimeOffset.UtcNow));

        var prices = store.GetAll().ToArray();

        Assert.Collection(
            prices,
            price => Assert.Equal("PETR4", price.Symbol),
            price => Assert.Equal("VALE3", price.Symbol));
    }

    [Fact]
    public async Task Store_supports_concurrent_updates_and_reads()
    {
        var store = new LatestPriceStore();
        var symbols = new[] { "PETR4", "VALE3", "ITUB4", "WEGE3", "PRIO3" };

        var tasks = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() =>
            {
                var symbol = symbols[index % symbols.Length];
                store.Update(new PriceUpdatedEvent(symbol, 1000 + index, DateTimeOffset.UtcNow));
                _ = store.GetAll();
            }));

        await Task.WhenAll(tasks);

        Assert.Equal(symbols.Length, store.GetAll().Count);
    }
}
