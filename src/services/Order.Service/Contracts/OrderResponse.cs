using MicroservicesLab.OrderService.Domain;

namespace MicroservicesLab.OrderService.Contracts;

public sealed record OrderResponse(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
