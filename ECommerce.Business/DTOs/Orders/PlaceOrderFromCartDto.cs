using System.ComponentModel.DataAnnotations;

namespace ECommerce.Business.DTOs.Orders;

public class PlaceOrderFromCartDto
{
    [Required]
    [StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;
}
