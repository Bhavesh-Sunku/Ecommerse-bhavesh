namespace ECommerce.OrderService.Models;

public class ShoppingCart
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public ICollection<ShoppingCartItem> Items { get; set; } = [];
}

public class ShoppingCartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }

    public ShoppingCart Cart { get; set; } = null!;

    public int ProductId { get; set; }

    public int Quantity { get; set; }
}

public class CustomerOrder
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public Guid? ReservationId { get; set; }

    public string? PaymentTransactionId { get; set; }

    public string? PaymentIdempotencyKey { get; set; }

    public ICollection<CustomerOrderItem> Items { get; set; } = [];
}

public class CustomerOrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public CustomerOrder Order { get; set; } = null!;

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductImageUrl { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
