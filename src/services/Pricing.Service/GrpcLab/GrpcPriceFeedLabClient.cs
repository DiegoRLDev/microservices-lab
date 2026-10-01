using Grpc.Core;
using Grpc.Net.Client;
using MicroservicesLab.Protos.PriceFeed;

namespace MicroservicesLab.PricingService.GrpcLab;

public sealed class GrpcPriceFeedLabClient(
    IConfiguration configuration,
    ILogger<GrpcPriceFeedLabClient> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var priceFeedAddress = configuration["GrpcLab:PriceFeedAddress"]
            ?? Environment.GetEnvironmentVariable("PRICE_FEED_GRPC_ADDRESS")
            ?? "http://localhost:9090";

        logger.LogInformation(
            "Pricing.Service gRPC lab client connecting to {PriceFeedAddress}",
            priceFeedAddress);

        using var channel = GrpcChannel.ForAddress(priceFeedAddress);
        var client = new PriceFeed.PriceFeedClient(channel);

        try
        {
            using var call = client.StreamPrices(new StreamPricesRequest(), cancellationToken: stoppingToken);

            await foreach (var update in call.ResponseStream.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation(
                    "[gRPC lab] {Symbol} {Price} {OccurredAt}",
                    update.Symbol,
                    FormatPrice(update.Price),
                    update.OccurredAt.ToDateTimeOffset());
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Pricing.Service gRPC lab client stopped.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Pricing.Service gRPC lab client stopped.");
        }
    }

    private static string FormatPrice(long cents)
    {
        var units = cents / 100;
        var remainder = Math.Abs(cents % 100);
        return $"{units}.{remainder:00}";
    }
}
