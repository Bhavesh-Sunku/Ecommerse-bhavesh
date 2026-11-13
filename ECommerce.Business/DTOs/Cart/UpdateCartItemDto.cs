using System.ComponentModel.DataAnnotations;

namespace ECommerce.Business.DTOs.Cart;

public class UpdateCartItemDto
{
    [Range(1, 1000)]
    public int Quantity { get; set; }
}
