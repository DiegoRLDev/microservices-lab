namespace MicroservicesLab.OrderService.Infrastructure.Grpc;

public interface IInventoryClient
{
    Task<InventoryAvailability> CheckAvailabilityAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken);
}

public sealed record InventoryAvailability(
    Guid ProductId,
    bool IsAvailable,
    int RequestedQuantity,
    int AvailableQuantity,
    decimal UnitPrice);

