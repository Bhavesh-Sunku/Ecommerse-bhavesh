using System.Security.Cryptography;
using System.Text;
using ECommerce.Payments.Contracts;

namespace ECommerce.Payments.Services;

public class MockPaymentProvider : IMockPaymentProvider
{
    public Task<MockProviderResult> ProcessAsync(
        int orderId,
        decimal amount,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (paymentMethod == "mock-decline")
        {
            return Task.FromResult(new MockProviderResult
            {
                Succeeded = false,
                FailureReason = "The mock provider declined this test payment."
            });
        }

        var transactionSeed = Encoding.UTF8.GetBytes($"{orderId}:{amount:0.00}");
        var transactionId = $"mock-{Convert.ToHexString(SHA256.HashData(transactionSeed))[..24]}";
        return Task.FromResult(new MockProviderResult
        {
            Succeeded = true,
            TransactionId = transactionId
        });
    }
}
