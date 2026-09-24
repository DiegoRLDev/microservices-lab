using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MicroservicesLab.InventoryService.Observability;

public static class InventoryTelemetry
{
    public const string ServiceName = "MicroservicesLab.InventoryService";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);
}
