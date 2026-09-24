using System.Diagnostics;
using System.Diagnostics.Metrics;
using MicroservicesLab.Observability;

namespace MicroservicesLab.AuditWorker.Observability;

public static class AuditTelemetry
{
    public const string ServiceName = "MicroservicesLab.AuditWorker";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly ActivitySource KafkaActivitySource = new(ServiceObservability.KafkaActivitySourceName);
    public static readonly Meter Meter = new(ServiceName);
}
