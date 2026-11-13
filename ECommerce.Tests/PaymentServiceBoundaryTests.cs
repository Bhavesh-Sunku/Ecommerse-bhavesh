using ECommerce.Payments.Context;
using ECommerce.Payments.Contracts;
using ECommerce.Payments.Models;
using ECommerce.Payments.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.Tests;

public class PaymentServiceBoundaryTests
{
    [Fact]
    public async Task CreateAsync_ConfirmsOrderOnlyAfterSuccessfulProviderResult()
    {
        await using var context = CreateContext();
        var (orderClient, provider) = CreateDependencies();
        var service = CreateService(context, orderClient.Object, provider.Object);

        var result = await service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None);

        Assert.Equal(PaymentStatuses.Successful, result.Status);
        Assert.True(result.OrderConfirmed);
        Assert.NotNull(result.TransactionId);
        orderClient.Verify(
            client => client.ConfirmPaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(PaymentStatuses.Successful, (await context.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnOrderOwnedByAnotherUser()
    {
        await using var context = CreateContext();
        var (orderClient, provider) = CreateDependencies();
        orderClient.Setup(client => client.GetOrderAsync(
                7,
                "Bearer user-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderSnapshot
            {
                Id = 7,
                UserId = 99,
                TotalAmount = 159.98m,
                Status = "Pending"
            });
        var service = CreateService(context, orderClient.Object, provider.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None));

        Assert.Empty(await context.Payments.ToListAsync());
        provider.Verify(
            item => item.ProcessAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsReusingKeyWithDifferentProviderMethod()
    {
        await using var context = CreateContext();
        context.Payments.Add(new PaymentRecord
        {
            OrderId = 7,
            UserId = 42,
            Amount = 159.98m,
            PaymentMethod = "mock-decline",
            IdempotencyKey = "checkout-request-1",
            Status = PaymentStatuses.Failed
        });
        await context.SaveChangesAsync();
        var (orderClient, provider) = CreateDependencies();
        var service = CreateService(context, orderClient.Object, provider.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest("mock-success"),
            CancellationToken.None));

        provider.Verify(
            item => item.ProcessAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RepeatedIdempotencyKeyDoesNotCallProviderAgain()
    {
        await using var context = CreateContext();
        var (orderClient, provider) = CreateDependencies();
        var service = CreateService(context, orderClient.Object, provider.Object);

        var first = await service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None);
        var replay = await service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None);

        Assert.Equal(first.TransactionId, replay.TransactionId);
        provider.Verify(
            item => item.ProcessAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        orderClient.Verify(
            client => client.ConfirmPaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_RetryCompletesOrderConfirmationAfterTransientFailure()
    {
        await using var context = CreateContext();
        var (orderClient, provider) = CreateDependencies();
        orderClient.SetupSequence(client => client.ConfirmPaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Temporary Order Service failure."))
            .Returns(Task.CompletedTask);
        var service = CreateService(context, orderClient.Object, provider.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None));

        var retry = await service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None);

        Assert.True(retry.OrderConfirmed);
        Assert.Single(await context.Payments.ToListAsync());
        provider.Verify(
            item => item.ProcessAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DeclinedMockPaymentDoesNotConfirmOrder()
    {
        await using var context = CreateContext();
        var (orderClient, provider) = CreateDependencies();
        provider.Setup(item => item.ProcessAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                "mock-decline",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockProviderResult
            {
                Succeeded = false,
                FailureReason = "Declined."
            });
        var service = CreateService(context, orderClient.Object, provider.Object);

        var response = await service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest("mock-decline"),
            CancellationToken.None);

        Assert.Equal(PaymentStatuses.Failed, response.Status);
        Assert.False(response.OrderConfirmed);
        orderClient.Verify(
            client => client.ConfirmPaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        orderClient.Verify(
            client => client.ReleasePaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RejectsReusingKeyForAnotherOrder()
    {
        await using var context = CreateContext();
        context.Payments.Add(new PaymentRecord
        {
            OrderId = 99,
            UserId = 42,
            Amount = 159.98m,
            PaymentMethod = "mock-success",
            IdempotencyKey = "checkout-request-1",
            Status = PaymentStatuses.Failed
        });
        await context.SaveChangesAsync();
        var (orderClient, provider) = CreateDependencies();
        var service = CreateService(context, orderClient.Object, provider.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            42,
            "Bearer user-token",
            CreateRequest(),
            CancellationToken.None));

        orderClient.Verify(
            client => client.GetOrderAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    private static PaymentManagementService CreateService(
        PaymentDbContext context,
        IOrderClient orderClient,
        IMockPaymentProvider paymentProvider)
    {
        return new PaymentManagementService(
            context,
            orderClient,
            paymentProvider,
            NullLogger<PaymentManagementService>.Instance);
    }

    private static (Mock<IOrderClient> OrderClient, Mock<IMockPaymentProvider> Provider)
        CreateDependencies()
    {
        var orderClient = new Mock<IOrderClient>();
        orderClient.Setup(client => client.GetOrderAsync(
                7,
                "Bearer user-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderSnapshot
            {
                Id = 7,
                UserId = 42,
                TotalAmount = 159.98m,
                Status = "Pending"
            });
        orderClient.Setup(client => client.ConfirmPaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderClient.Setup(client => client.PreparePaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderClient.Setup(client => client.ReleasePaymentAsync(
                It.IsAny<OrderSnapshot>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = new Mock<IMockPaymentProvider>();
        provider.Setup(item => item.ProcessAsync(
                7,
                159.98m,
                "mock-success",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockProviderResult
            {
                Succeeded = true,
                TransactionId = "mock-transaction-7"
            });
        return (orderClient, provider);
    }

    private static CreatePaymentRequest CreateRequest(string paymentMethod = "mock-success")
    {
        return new CreatePaymentRequest
        {
            OrderId = 7,
            IdempotencyKey = "checkout-request-1",
            PaymentMethod = paymentMethod
        };
    }
}
