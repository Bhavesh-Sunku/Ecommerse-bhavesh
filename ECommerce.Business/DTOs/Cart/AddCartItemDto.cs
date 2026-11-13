using System.ComponentModel.DataAnnotations;

namespace ECommerce.Business.DTOs.Cart;

public class AddCartItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}
