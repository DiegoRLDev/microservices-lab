namespace MicroservicesLab.InventoryService.Contracts;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    decimal Price,
    int AvailableQuantity);

