namespace MicroservicesLab.OrderService.Infrastructure.Grpc;

public sealed class InventoryClientException(string message, Exception innerException)
    : Exception(message, innerException);

