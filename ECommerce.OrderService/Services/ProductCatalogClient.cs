using System.Net;
using System.Net.Http.Json;
using ECommerce.OrderService.Contracts;

namespace ECommerce.OrderService.Services;

public class ProductCatalogClient(HttpClient client)
{
    public async Task<ProductSummary?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"api/products/{productId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductSummary>(
            cancellationToken: cancellationToken);
    }
}
