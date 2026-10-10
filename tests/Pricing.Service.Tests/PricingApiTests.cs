using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicroservicesLab.PricingService.Contracts;
using MicroservicesLab.PricingService.Services;

namespace MicroservicesLab.PricingService.Tests;

public sealed class PricingApiTests
{
    [Fact]
    public async Task GetPrices_returns_latest_prices()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<LatestPriceStore>();
        store.Update(new PriceUpdatedEvent("PETR4", 3815, DateTimeOffset.UtcNow));

        var client = factory.CreateClient();

        var prices = await client.GetFromJsonAsync<PriceResponse[]>("/prices");

        Assert.NotNull(prices);
        var price = Assert.Single(prices);
        Assert.Equal("PETR4", price.Symbol);
        Assert.Equal(3815, price.Price);
    }

    [Fact]
    public async Task GetPriceBySymbol_returns_not_found_when_symbol_is_unknown()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/prices/UNKNOWN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPriceBySymbol_returns_latest_price()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<LatestPriceStore>();
        store.Update(new PriceUpdatedEvent("PETR4", 3815, DateTimeOffset.UtcNow));

        var client = factory.CreateClient();

        var price = await client.GetFromJsonAsync<PriceResponse>("/prices/petr4");

        Assert.NotNull(price);
        Assert.Equal("PETR4", price.Symbol);
        Assert.Equal(3815, price.Price);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Kafka:Enabled"] = "false",
                        ["GrpcLab:Enabled"] = "false"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var hostedServices = services
                        .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                        .ToArray();

                    foreach (var hostedService in hostedServices)
                    {
                        services.Remove(hostedService);
                    }
                });
            });
    }
}
