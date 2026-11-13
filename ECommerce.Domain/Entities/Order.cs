using System;
using System.Collections.Generic;
using System.Text;

using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public class Order
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public User User { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();

    public Payment? Payment { get; set; }
}