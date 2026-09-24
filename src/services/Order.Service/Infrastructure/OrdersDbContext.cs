using Microsoft.EntityFrameworkCore;
using MicroservicesLab.OrderService.Domain;

namespace MicroservicesLab.OrderService.Infrastructure;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(order => order.Id);

            entity.Property(order => order.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(order => order.ProductId)
                .HasColumnName("product_id")
                .IsRequired();

            entity.Property(order => order.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            entity.Property(order => order.UnitPrice)
                .HasColumnName("unit_price")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.TotalPrice)
                .HasColumnName("total_price")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(order => order.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.HasIndex(order => order.ProductId)
                .HasDatabaseName("ix_orders_product_id");

            entity.HasIndex(order => order.CreatedAt)
                .HasDatabaseName("ix_orders_created_at");
        });
    }
}
