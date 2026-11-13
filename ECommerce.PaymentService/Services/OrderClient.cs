using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ECommerce.Payments.Contracts;
using ECommerce.Payments.Models;

namespace ECommerce.Payments.Services;

public class OrderClient : IOrderClient
{
    private readonly HttpClient _httpClient;
    private readonly string _internalServiceKey;

    public OrderClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _internalServiceKey = configuration["PaymentService:InternalServiceKey"]
            ?? throw new InvalidOperationException("Payment service internal key is not configured.");
        if (Encoding.UTF8.GetByteCount(_internalServiceKey) < 32)
        {
            throw new InvalidOperationException(
                "Payment service internal key must contain at least 32 UTF-8 bytes.");
        }
    }

    public async Task<OrderSnapshot?> GetOrderAsync(
        int orderId,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/orders/{orderId}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(bearerToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new OrderServiceException("Order authorization failed.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OrderServiceException(
                "Order Service could not retrieve the order.",
                (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<OrderSnapshot>(
            cancellationToken: cancellationToken);
    }

    public async Task ConfirmPaymentAsync(
        OrderSnapshot order,
        string transactionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "internal/payments/confirm")
        {
            Content = JsonContent.Create(CreateReservationRequest(order, idempotencyKey, transactionId))
        };
        request.Headers.TryAddWithoutValidation("X-Payment-Service-Key", _internalServiceKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new OrderServiceException("Order no longer exists.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OrderServiceException(
                "Order Service did not confirm the payment.",
                (int)response.StatusCode);
        }
    }

    public Task PreparePaymentAsync(
        OrderSnapshot order,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return SendPaymentCommandAsync(
            "internal/payments/prepare",
            CreateReservationRequest(order, idempotencyKey),
            cancellationToken);
    }

    public Task ReleasePaymentAsync(
        OrderSnapshot order,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return SendPaymentCommandAsync(
            "internal/payments/release",
            CreateReservationRequest(order, idempotencyKey),
            cancellationToken);
    }

    private async Task SendPaymentCommandAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("X-Payment-Service-Key", _internalServiceKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new OrderServiceException("Order no longer exists.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OrderServiceException(
                "Order Service rejected a payment operation.",
                (int)response.StatusCode);
        }
    }

    private static object CreateReservationRequest(
        OrderSnapshot order,
        string idempotencyKey,
        string? transactionId = null)
    {
        var reservation = new
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Amount = order.TotalAmount,
            IdempotencyKey = idempotencyKey
        };
        return transactionId is null
            ? reservation
            : new
            {
                reservation.OrderId,
                reservation.UserId,
                reservation.Amount,
                reservation.IdempotencyKey,
                TransactionId = transactionId
            };
    }
}
