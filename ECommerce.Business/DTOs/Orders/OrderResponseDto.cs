using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Domain.Enums;

namespace ECommerce.Business.DTOs.Orders;

public class OrderResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public List<OrderItemResponseDto> Items { get; set; }
        = new List<OrderItemResponseDto>();
}