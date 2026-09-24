using Microsoft.EntityFrameworkCore;
using MicroservicesLab.InventoryService.Domain;

namespace MicroservicesLab.InventoryService.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(product => product.Id);

            entity.Property(product => product.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(product => product.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(product => product.Price)
                .HasColumnName("price")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(product => product.AvailableQuantity)
                .HasColumnName("available_quantity")
                .IsRequired();

            entity.HasIndex(product => product.Name)
                .HasDatabaseName("ix_products_name");
        });
    }
}

