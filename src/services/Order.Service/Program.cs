using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using MicroservicesLab.OrderService.Contracts;
using MicroservicesLab.OrderService.Infrastructure.Grpc;
using MicroservicesLab.OrderService.Infrastructure;
using MicroservicesLab.OrderService.Infrastructure.Kafka;
using MicroservicesLab.OrderService.Infrastructure.Resilience;
using MicroservicesLab.OrderService.Observability;
using MicroservicesLab.OrderService.Services;
using MicroservicesLab.Observability;
using MicroservicesLab.Protos.Inventory;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddTraceCorrelationToConsole();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrdersDbContext>("orders-db");
builder.Services.AddMicroservicesLabObservability(
    builder.Configuration,
    OrderTelemetry.ServiceName,
    includeAspNetCore: true,
    OrderTelemetry.ServiceName);

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrdersDb")));

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<InventoryResilienceOptions>(builder.Configuration.GetSection("Resilience:Inventory"));

builder.Services.AddGrpcClient<Inventory.InventoryClient>(options =>
{
    options.Address = new Uri(builder.Configuration["InventoryGrpc:Address"]
        ?? throw new InvalidOperationException("InventoryGrpc:Address is required."));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InventoryCircuitBreaker>();
builder.Services.AddScoped<IInventoryClient, GrpcInventoryClient>();
builder.Services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();
builder.Services.AddScoped<OrdersAppService>();
builder.Services.AddScoped<PaymentEventsAppService>();
builder.Services.AddHostedService<PaymentEventsConsumer>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

var orders = app.MapGroup("/orders").WithTags("Orders");

orders.MapPost("", async (
    CreateOrderRequest request,
    OrdersAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(request, cancellationToken);

    return result.Match<IResult>(
        created => Results.Created($"/orders/{created.Id}", created),
        (error, errorType) => errorType == OperationErrorType.DependencyUnavailable
            ? Results.Json(new ProblemDetailsResponse(error), statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.BadRequest(new ProblemDetailsResponse(error)));
})
.WithName("CreateOrder");

orders.MapGet("/{id:guid}", async (
    Guid id,
    OrdersAppService service,
    CancellationToken cancellationToken) =>
{
    var order = await service.GetByIdAsync(id, cancellationToken);

    return order is null ? Results.NotFound() : Results.Ok(order);
})
.WithName("GetOrderById");

orders.MapGet("", async (
    OrdersAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ListAsync(cancellationToken);

    return Results.Ok(result);
})
.WithName("ListOrders");

app.Run();

public partial class Program;
