namespace ECommerce.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Category { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties

    public ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();

    public ICollection<CartItem> CartItems { get; set; }
        = new List<CartItem>();

    public Inventory? Inventory { get; set; }
}