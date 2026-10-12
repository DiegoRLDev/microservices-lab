using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Contracts;
using MicroservicesLab.AuditWorker.Infrastructure;
using MicroservicesLab.AuditWorker.Infrastructure.Kafka;
using MicroservicesLab.AuditWorker.Observability;
using MicroservicesLab.AuditWorker.Services;
using MicroservicesLab.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddTraceCorrelationToConsole();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuditDbContext>("audit-db");
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuditDb")));
builder.Services.AddMicroservicesLabObservability(
    builder.Configuration,
    AuditTelemetry.ServiceName,
    includeAspNetCore: true,
    AuditTelemetry.ServiceName);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddScoped<AuditEventProcessor>();
builder.Services.AddScoped<AuditEventsQueryService>();
builder.Services.AddHostedService<KafkaAuditConsumer>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.MapGet("/audit-events", async (
    string? eventType,
    string? messageKey,
    string? orderId,
    int? limit,
    int? offset,
    AuditEventsQueryService service,
    CancellationToken cancellationToken) =>
{
    var requestedLimit = limit ?? AuditEventsQueryService.DefaultLimit;
    var requestedOffset = offset ?? 0;

    if (requestedLimit <= 0 || requestedLimit > AuditEventsQueryService.MaxLimit)
    {
        return Results.BadRequest(new ProblemDetailsResponse(
            $"limit must be between 1 and {AuditEventsQueryService.MaxLimit}."));
    }

    if (requestedOffset < 0)
    {
        return Results.BadRequest(new ProblemDetailsResponse("offset cannot be negative."));
    }

    if (!string.IsNullOrWhiteSpace(messageKey) && !string.IsNullOrWhiteSpace(orderId) && messageKey != orderId)
    {
        return Results.BadRequest(new ProblemDetailsResponse("messageKey and orderId filters must match when both are provided."));
    }

    var query = new AuditEventQuery(
        eventType,
        string.IsNullOrWhiteSpace(messageKey) ? orderId : messageKey,
        requestedLimit,
        requestedOffset);

    var events = await service.ListAsync(query, cancellationToken);

    return Results.Ok(events);
})
.WithName("ListAuditEvents");

await app.RunAsync();
