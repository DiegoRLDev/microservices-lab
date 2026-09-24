namespace MicroservicesLab.OrderService.Domain;

public sealed class Order
{
    private Order()
    {
    }

    public Order(Guid productId, int quantity, decimal unitPrice, DateTimeOffset createdAt)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId must be provided.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "UnitPrice cannot be negative.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = quantity * unitPrice;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal TotalPrice { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool MarkAsPaid()
    {
        if (Status == OrderStatus.Paid)
        {
            return false;
        }

        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Order {Id} cannot be marked as paid from status {Status}.");
        }

        Status = OrderStatus.Paid;
        return true;
    }

    public bool RejectPayment()
    {
        if (Status == OrderStatus.PaymentRejected)
        {
            return false;
        }

        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Order {Id} cannot be marked as payment rejected from status {Status}.");
        }

        Status = OrderStatus.PaymentRejected;
        return true;
    }
}
