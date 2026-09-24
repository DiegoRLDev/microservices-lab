using Grpc.Core;
using System.Globalization;
using MicroservicesLab.InventoryService.Services;
using MicroservicesLab.Protos.Inventory;

namespace MicroservicesLab.InventoryService.Grpc;

public sealed class InventoryGrpcService(InventoryAppService inventoryService, ILogger<InventoryGrpcService> logger)
    : Inventory.InventoryBase
{
    public override async Task<CheckAvailabilityResponse> CheckAvailability(
        CheckAvailabilityRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be a valid GUID."));
        }

        var result = await inventoryService.CheckAvailabilityAsync(
            productId,
            request.Quantity,
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            throw ToRpcException(result.Error!);
        }

        var availability = result.Value!;

        logger.LogInformation(
            "Checked availability for product {ProductId}. Requested {RequestedQuantity}, available {AvailableQuantity}, result {IsAvailable}",
            availability.ProductId,
            availability.RequestedQuantity,
            availability.AvailableQuantity,
            availability.IsAvailable);

        return new CheckAvailabilityResponse
        {
            ProductId = availability.ProductId.ToString(),
            IsAvailable = availability.IsAvailable,
            RequestedQuantity = availability.RequestedQuantity,
            AvailableQuantity = availability.AvailableQuantity,
            UnitPrice = availability.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<ReserveStockResponse> ReserveStock(
        ReserveStockRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be a valid GUID."));
        }

        var result = await inventoryService.ReserveStockAsync(
            productId,
            request.Quantity,
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            throw ToRpcException(result.Error!);
        }

        var reservation = result.Value!;

        logger.LogInformation(
            "Reserved stock through gRPC for product {ProductId}. Reserved {ReservedQuantity}, remaining {RemainingQuantity}",
            reservation.ProductId,
            reservation.ReservedQuantity,
            reservation.RemainingQuantity);

        return new ReserveStockResponse
        {
            ProductId = reservation.ProductId.ToString(),
            ReservedQuantity = reservation.ReservedQuantity,
            RemainingQuantity = reservation.RemainingQuantity,
            UnitPrice = reservation.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)
        };
    }

    private static RpcException ToRpcException(string error)
    {
        var statusCode = error switch
        {
            "Product was not found." => StatusCode.NotFound,
            "Insufficient stock." => StatusCode.FailedPrecondition,
            _ => StatusCode.InvalidArgument
        };

        return new RpcException(new Status(statusCode, error));
    }
}
