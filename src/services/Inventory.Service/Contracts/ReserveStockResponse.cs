namespace MicroservicesLab.InventoryService.Contracts;

public sealed record ReserveStockResponse(
    Guid ProductId,
    int ReservedQuantity,
    int RemainingQuantity,
    decimal UnitPrice);

