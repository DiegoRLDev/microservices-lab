using MicroservicesLab.PricingService.GrpcLab;
using MicroservicesLab.PricingService.Infrastructure.Kafka;
using MicroservicesLab.PricingService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<LatestPriceStore>();

var grpcLabEnabled = builder.Configuration.GetValue<bool>("GrpcLab:Enabled");
var kafkaEnabled = builder.Configuration.GetValue<bool?>("Kafka:Enabled") ?? !grpcLabEnabled;

if (grpcLabEnabled)
{
    builder.Services.AddHostedService<GrpcPriceFeedLabClient>();
}
else if (kafkaEnabled)
{
    builder.Services.AddHostedService<KafkaPriceUpdatedConsumer>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

var prices = app.MapGroup("/prices").WithTags("Prices");

prices.MapGet("", (LatestPriceStore store) =>
{
    return Results.Ok(store.GetAll());
})
.WithName("GetPrices");

prices.MapGet("/{symbol}", (string symbol, LatestPriceStore store) =>
{
    var price = store.Get(symbol);

    return price is null ? Results.NotFound() : Results.Ok(price);
})
.WithName("GetPriceBySymbol");

app.Run();

public partial class Program;
