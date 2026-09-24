using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Infrastructure;
using MicroservicesLab.AuditWorker.Infrastructure.Kafka;
using MicroservicesLab.AuditWorker.Observability;
using MicroservicesLab.AuditWorker.Services;
using MicroservicesLab.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddTraceCorrelationToConsole();

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuditDb")));
builder.Services.AddMicroservicesLabObservability(
    builder.Configuration,
    AuditTelemetry.ServiceName,
    includeAspNetCore: false,
    AuditTelemetry.ServiceName);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddScoped<AuditEventProcessor>();
builder.Services.AddHostedService<KafkaAuditConsumer>();

var host = builder.Build();

if (host.Services.GetRequiredService<IConfiguration>().GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
