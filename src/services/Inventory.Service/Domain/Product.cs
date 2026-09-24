namespace MicroservicesLab.InventoryService.Domain;

public sealed class Product
{
    private Product()
    {
        Name = string.Empty;
    }

    public Product(string name, decimal price, int availableQuantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name must be provided.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        if (availableQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableQuantity), "AvailableQuantity cannot be negative.");
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Price = price;
        AvailableQuantity = availableQuantity;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public decimal Price { get; private set; }

    public int AvailableQuantity { get; private set; }

    public bool HasAvailableStock(int quantity)
    {
        return quantity > 0 && AvailableQuantity >= quantity;
    }

    public bool Reserve(int quantity)
    {
        if (!HasAvailableStock(quantity))
        {
            return false;
        }

        AvailableQuantity -= quantity;

        return true;
    }
}

