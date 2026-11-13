namespace ECommerce.ProductService.Models;

public class InventoryReservation
{
    public int Id { get; set; }

    public Guid ReservationId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public string Status { get; set; } = "Reserved";

    public DateTime CreatedAt { get; set; }
}
