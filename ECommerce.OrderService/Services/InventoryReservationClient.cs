using System.Net.Http.Json;

namespace ECommerce.OrderService.Services;

public class InventoryReservationClient(
    HttpClient client,
    IConfiguration configuration)
{
    public async Task ReserveAsync(
        Guid reservationId,
        IReadOnlyCollection<(int ProductId, int Quantity)> items,
        CancellationToken cancellationToken)
    {
        var serviceKey = configuration["ProductService:InternalServiceKey"]
            ?? throw new InvalidOperationException(
                "Product Service internal API key is not configured.");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "internal/inventory/reservations")
        {
            Content = JsonContent.Create(new
            {
                ReservationId = reservationId,
                Items = items.Select(item => new
                {
                    item.ProductId,
                    item.Quantity
                })
            })
        };
        request.Headers.Add("X-Internal-Service-Key", serviceKey);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var serviceKey = configuration["ProductService:InternalServiceKey"]
            ?? throw new InvalidOperationException(
                "Product Service internal API key is not configured.");
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"internal/inventory/reservations/{reservationId}");
        request.Headers.Add("X-Internal-Service-Key", serviceKey);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
