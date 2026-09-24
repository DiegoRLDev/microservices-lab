using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using MicroservicesLab.InventoryService.Contracts;
using MicroservicesLab.InventoryService.Grpc;
using MicroservicesLab.InventoryService.Infrastructure;
using MicroservicesLab.InventoryService.Observability;
using MicroservicesLab.InventoryService.Services;
using MicroservicesLab.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddTraceCorrelationToConsole();

builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>("inventory-db");
builder.Services.AddMicroservicesLabObservability(
    builder.Configuration,
    InventoryTelemetry.ServiceName,
    includeAspNetCore: true,
    InventoryTelemetry.ServiceName);

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDb")));

builder.Services.AddScoped<InventoryAppService>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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
app.MapGrpcService<InventoryGrpcService>();

var products = app.MapGroup("/products").WithTags("Products");

products.MapPost("", async (
    CreateProductRequest request,
    InventoryAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateProductAsync(request, cancellationToken);

    return result.Match<IResult>(
        created => Results.Created($"/products/{created.Id}", created),
        error => Results.BadRequest(new ProblemDetailsResponse(error)));
})
.WithName("CreateProduct");

products.MapGet("", async (
    InventoryAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ListProductsAsync(cancellationToken);

    return Results.Ok(result);
})
.WithName("ListProducts");

products.MapGet("/{id:guid}", async (
    Guid id,
    InventoryAppService service,
    CancellationToken cancellationToken) =>
{
    var product = await service.GetProductByIdAsync(id, cancellationToken);

    return product is null ? Results.NotFound() : Results.Ok(product);
})
.WithName("GetProductById");

products.MapPost("/{id:guid}/availability", async (
    Guid id,
    CheckAvailabilityRequest request,
    InventoryAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CheckAvailabilityAsync(id, request.Quantity, cancellationToken);

    return result.Match<IResult>(
        availability => Results.Ok(availability),
        error => Results.BadRequest(new ProblemDetailsResponse(error)));
})
.WithName("CheckProductAvailability");

products.MapPost("/{id:guid}/reserve", async (
    Guid id,
    ReserveStockRequest request,
    InventoryAppService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ReserveStockAsync(id, request.Quantity, cancellationToken);

    return result.Match<IResult>(
        reservation => Results.Ok(reservation),
        error => Results.BadRequest(new ProblemDetailsResponse(error)));
})
.WithName("ReserveProductStock");

app.Run();

public partial class Program;
