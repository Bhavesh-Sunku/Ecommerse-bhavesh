using ECommerce.Payments.Contracts;

namespace ECommerce.Payments.Services;

public interface IOrderClient
{
    Task<OrderSnapshot?> GetOrderAsync(
        int orderId,
        string bearerToken,
        CancellationToken cancellationToken);

    Task PreparePaymentAsync(
        OrderSnapshot order,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task ConfirmPaymentAsync(
        OrderSnapshot order,
        string transactionId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task ReleasePaymentAsync(
        OrderSnapshot order,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface IMockPaymentProvider
{
    Task<MockProviderResult> ProcessAsync(
        int orderId,
        decimal amount,
        string paymentMethod,
        CancellationToken cancellationToken);
}

public sealed class OrderServiceException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
