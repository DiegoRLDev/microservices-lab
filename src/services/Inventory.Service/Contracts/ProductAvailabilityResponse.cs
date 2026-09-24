namespace MicroservicesLab.InventoryService.Contracts;

public sealed record ProductAvailabilityResponse(
    Guid ProductId,
    bool IsAvailable,
    int RequestedQuantity,
    int AvailableQuantity,
    decimal UnitPrice);

