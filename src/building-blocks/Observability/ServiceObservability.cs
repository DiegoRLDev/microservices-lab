using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MicroservicesLab.Observability;

public static class ServiceObservability
{
    public const string KafkaActivitySourceName = "MicroservicesLab.Kafka";

    public static IServiceCollection AddMicroservicesLabObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCore = true,
        params string[] meters)
    {
        var tracingSources = new[]
        {
            serviceName,
            KafkaActivitySourceName
        };

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? "Development")
                ]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(tracingSources)
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    .AddGrpcClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddOtlpExporter();

                if (includeAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation(options => options.RecordException = true);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(meters)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddOtlpExporter();

                if (includeAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }
            });

        return services;
    }

    public static ILoggingBuilder AddTraceCorrelationToConsole(this ILoggingBuilder logging)
    {
        logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });

        return logging;
    }
}
