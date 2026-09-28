using Grpc.Core;
using Grpc.Net.Client;
using MicroservicesLab.Protos.PriceFeed;

var priceFeedAddress = Environment.GetEnvironmentVariable("PRICE_FEED_GRPC_ADDRESS")
    ?? "http://localhost:9090";

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"Pricing.Service gRPC lab client connecting to {priceFeedAddress}");

using var channel = GrpcChannel.ForAddress(priceFeedAddress);
var client = new PriceFeed.PriceFeedClient(channel);

try
{
    using var call = client.StreamPrices(new StreamPricesRequest(), cancellationToken: shutdown.Token);

    await foreach (var update in call.ResponseStream.ReadAllAsync(shutdown.Token))
    {
        Console.WriteLine(
            $"[gRPC] {update.Symbol} {FormatPrice(update.Price)} {update.OccurredAt.ToDateTime():O}");
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("Pricing.Service gRPC lab client stopped.");
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && shutdown.IsCancellationRequested)
{
    Console.WriteLine("Pricing.Service gRPC lab client stopped.");
}

static string FormatPrice(long cents)
{
    var units = cents / 100;
    var remainder = Math.Abs(cents % 100);
    return $"{units}.{remainder:00}";
}
