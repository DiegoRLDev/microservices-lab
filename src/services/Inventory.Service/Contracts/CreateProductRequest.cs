namespace MicroservicesLab.InventoryService.Contracts;

public sealed record CreateProductRequest(
    string Name,
    decimal Price,
    int AvailableQuantity);

