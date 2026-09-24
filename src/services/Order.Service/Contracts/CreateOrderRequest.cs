namespace MicroservicesLab.OrderService.Contracts;

public sealed record CreateOrderRequest(Guid ProductId, int Quantity);
