namespace ECommerce.Business.DTOs.Cart;

public class CartResponseDto
{
    public int Id { get; set; }

    public List<CartItemResponseDto> Items { get; set; } = [];

    public decimal TotalAmount => Items.Sum(item => item.Subtotal);
}
