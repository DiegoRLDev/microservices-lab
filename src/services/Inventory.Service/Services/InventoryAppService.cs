using Microsoft.EntityFrameworkCore;
using MicroservicesLab.InventoryService.Contracts;
using MicroservicesLab.InventoryService.Domain;
using MicroservicesLab.InventoryService.Infrastructure;

namespace MicroservicesLab.InventoryService.Services;

public sealed class InventoryAppService(InventoryDbContext dbContext, ILogger<InventoryAppService> logger)
{
    public async Task<OperationResult<ProductResponse>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<ProductResponse>.Failure("Product name must be provided.");
        }

        if (request.Price < 0)
        {
            return OperationResult<ProductResponse>.Failure("Price cannot be negative.");
        }

        if (request.AvailableQuantity < 0)
        {
            return OperationResult<ProductResponse>.Failure("AvailableQuantity cannot be negative.");
        }

        var product = new Product(request.Name, request.Price, request.AvailableQuantity);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created product {ProductId} with available quantity {AvailableQuantity}",
            product.Id,
            product.AvailableQuantity);

        return OperationResult<ProductResponse>.Success(product.ToResponse());
    }

    public async Task<ProductResponse?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => product.ToResponse())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductResponse>> ListProductsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(product => product.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<ProductAvailabilityResponse>> CheckAvailabilityAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            return OperationResult<ProductAvailabilityResponse>.Failure("ProductId must be provided.");
        }

        if (quantity <= 0)
        {
            return OperationResult<ProductAvailabilityResponse>.Failure("Quantity must be greater than zero.");
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

        if (product is null)
        {
            return OperationResult<ProductAvailabilityResponse>.Failure("Product was not found.");
        }

        return OperationResult<ProductAvailabilityResponse>.Success(new ProductAvailabilityResponse(
            product.Id,
            product.HasAvailableStock(quantity),
            quantity,
            product.AvailableQuantity,
            product.Price));
    }

    public async Task<OperationResult<ReserveStockResponse>> ReserveStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            return OperationResult<ReserveStockResponse>.Failure("ProductId must be provided.");
        }

        if (quantity <= 0)
        {
            return OperationResult<ReserveStockResponse>.Failure("Quantity must be greater than zero.");
        }

        var product = await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

        if (product is null)
        {
            return OperationResult<ReserveStockResponse>.Failure("Product was not found.");
        }

        if (!product.Reserve(quantity))
        {
            return OperationResult<ReserveStockResponse>.Failure("Insufficient stock.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reserved {ReservedQuantity} units for product {ProductId}. Remaining quantity: {RemainingQuantity}",
            quantity,
            product.Id,
            product.AvailableQuantity);

        return OperationResult<ReserveStockResponse>.Success(new ReserveStockResponse(
            product.Id,
            quantity,
            product.AvailableQuantity,
            product.Price));
    }
}

internal static class ProductMappings
{
    public static ProductResponse ToResponse(this Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Price,
            product.AvailableQuantity);
    }
}

