using System.ComponentModel.DataAnnotations;

namespace ECommerce.OrderService.Contracts;

public class AddCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}

public class UpdateCartItemRequest
{
    [Range(1, 100)]
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    [Required]
    [StringLength(1000)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class PaymentReservationRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class ConfirmPaymentRequest : PaymentReservationRequest
{
    [Required]
    [StringLength(100)]
    public string TransactionId { get; set; } = string.Empty;
}

public class ProductSummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int AvailableStock { get; set; }

    public bool IsActive { get; set; }
}

public class CartResponse
{
    public int Id { get; set; }

    public List<CartLineResponse> Items { get; set; } = [];

    public decimal TotalAmount { get; set; }
}

public class CartLineResponse
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductImageUrl { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public int AvailableStock { get; set; }

    public decimal Subtotal { get; set; }
}

public class OrderSummaryResponse
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<CartLineResponse> Items { get; set; } = [];
}
