using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroservicesLab.InventoryService.Contracts;
using MicroservicesLab.InventoryService.Infrastructure;
using MicroservicesLab.InventoryService.Services;

namespace MicroservicesLab.InventoryService.Tests;

public sealed class InventoryAppServiceTests
{
    [Fact]
    public async Task CreateProductAsync_creates_product()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryAppService(dbContext, NullLogger<InventoryAppService>.Instance);

        var result = await service.CreateProductAsync(
            new CreateProductRequest("Notebook", 3500.50m, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal("Notebook", result.Value.Name);
        Assert.Equal(3500.50m, result.Value.Price);
        Assert.Equal(10, result.Value.AvailableQuantity);
        Assert.Equal(1, await dbContext.Products.CountAsync());
    }

    [Fact]
    public async Task CheckAvailabilityAsync_returns_available_when_quantity_exists()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryAppService(dbContext, NullLogger<InventoryAppService>.Instance);
        var product = await service.CreateProductAsync(
            new CreateProductRequest("Mouse", 99.90m, 5),
            CancellationToken.None);

        var result = await service.CheckAvailabilityAsync(product.Value!.Id, 3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsAvailable);
        Assert.Equal(3, result.Value.RequestedQuantity);
        Assert.Equal(5, result.Value.AvailableQuantity);
        Assert.Equal(99.90m, result.Value.UnitPrice);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_returns_unavailable_when_quantity_is_greater_than_stock()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryAppService(dbContext, NullLogger<InventoryAppService>.Instance);
        var product = await service.CreateProductAsync(
            new CreateProductRequest("Keyboard", 250m, 2),
            CancellationToken.None);

        var result = await service.CheckAvailabilityAsync(product.Value!.Id, 3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsAvailable);
        Assert.Equal(2, result.Value.AvailableQuantity);
    }

    [Fact]
    public async Task ReserveStockAsync_decrements_available_quantity()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryAppService(dbContext, NullLogger<InventoryAppService>.Instance);
        var product = await service.CreateProductAsync(
            new CreateProductRequest("Monitor", 1200m, 4),
            CancellationToken.None);

        var result = await service.ReserveStockAsync(product.Value!.Id, 3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.ReservedQuantity);
        Assert.Equal(1, result.Value.RemainingQuantity);
        Assert.Equal(1, (await dbContext.Products.SingleAsync()).AvailableQuantity);
    }

    [Fact]
    public async Task ReserveStockAsync_rejects_when_stock_is_insufficient()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryAppService(dbContext, NullLogger<InventoryAppService>.Instance);
        var product = await service.CreateProductAsync(
            new CreateProductRequest("Webcam", 450m, 1),
            CancellationToken.None);

        var result = await service.ReserveStockAsync(product.Value!.Id, 2, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient stock.", result.Error);
        Assert.Equal(1, (await dbContext.Products.SingleAsync()).AvailableQuantity);
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }
}

