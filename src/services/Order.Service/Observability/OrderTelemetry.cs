using System.Diagnostics;
using System.Diagnostics.Metrics;
using MicroservicesLab.Observability;

namespace MicroservicesLab.OrderService.Observability;

public static class OrderTelemetry
{
    public const string ServiceName = "MicroservicesLab.OrderService";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly ActivitySource KafkaActivitySource = new(ServiceObservability.KafkaActivitySourceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> OrdersCreated = Meter.CreateCounter<long>(
        "orders_created_total",
        description: "Total number of orders successfully created.");

    public static readonly Counter<long> OrdersRejected = Meter.CreateCounter<long>(
        "orders_rejected_total",
        description: "Total number of rejected order creation attempts.");

    public static readonly Counter<long> InventoryCalls = Meter.CreateCounter<long>(
        "inventory_calls_total",
        description: "Total number of inventory availability checks called by Order.Service.");

    public static readonly Counter<long> InventoryFailures = Meter.CreateCounter<long>(
        "inventory_dependency_failures_total",
        description: "Total number of failed inventory dependency calls observed by Order.Service.");

    public static readonly Counter<long> CircuitBreakerOpens = Meter.CreateCounter<long>(
        "inventory_circuit_breaker_opens_total",
        description: "Total number of times the inventory circuit breaker opened.");

    public static readonly Counter<long> PaymentEventsConsumed = Meter.CreateCounter<long>(
        "payment_events_consumed_total",
        description: "Total number of payment result events consumed by Order.Service.");

    public static void RecordOrderRejected(string reason)
    {
        var tags = new TagList
        {
            { "reason", reason }
        };

        OrdersRejected.Add(1, tags);
    }

    public static void RecordInventoryCall(string result)
    {
        var tags = new TagList
        {
            { "result", result }
        };

        InventoryCalls.Add(1, tags);
    }

    public static void RecordInventoryFailure(string reason)
    {
        var tags = new TagList
        {
            { "reason", reason }
        };

        InventoryFailures.Add(1, tags);
    }

    public static void RecordPaymentEventConsumed(string eventType)
    {
        var tags = new TagList
        {
            { "event.type", eventType }
        };

        PaymentEventsConsumed.Add(1, tags);
    }
}
