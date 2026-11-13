using System.Net;
using System.Text;
using ECommerce.OrderService.Context;
using ECommerce.OrderService.Contracts;
using ECommerce.OrderService.Models;
using ECommerce.OrderService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests;

public class OrderServiceBoundaryTests
{
    [Fact]
    public async Task PlaceOrderAsync_SnapshotsProductAndClearsCart()
    {
        await using var context = CreateContext();
        context.Carts.Add(new ShoppingCart
        {
            UserId = 42,
            Items =
            [
                new ShoppingCartItem { ProductId = 7, Quantity = 2 }
            ]
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, StockResponse(5));
        var order = await service.PlaceOrderAsync(
            42,
            new PlaceOrderRequest
            {
                ShippingAddress = "123 Demo Street",
                PhoneNumber = "+1 555 0100"
            },
            "Demo Customer",
            CancellationToken.None);

        Assert.Equal(159.98m, order.TotalAmount);
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Trail Shoes", Assert.Single(order.Items).ProductName);
        Assert.Empty(await context.CartItems.ToListAsync());
        Assert.Single(await context.Orders.ToListAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_ReleasesReservationWhenReserveResponseIsLost()
    {
        await using var context = CreateContext();
        context.Carts.Add(new ShoppingCart
        {
            UserId = 42,
            Items = [new ShoppingCartItem { ProductId = 7, Quantity = 2 }]
        });
        await context.SaveChangesAsync();

        var reservationHandler = new AmbiguousReservationHandler();
        var service = CreateService(
            context,
            StockResponse(5),
            reservationHandler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.PlaceOrderAsync(
                42,
                new PlaceOrderRequest
                {
                    ShippingAddress = "123 Demo Street",
                    PhoneNumber = "+1 555 0100"
                },
                "Demo Customer",
                CancellationToken.None));

        Assert.True(reservationHandler.ReleaseAttempted);
        Assert.False(reservationHandler.ReservationExists);
        Assert.Empty(await context.Orders.ToListAsync());
        Assert.Single(await context.CartItems.ToListAsync());
    }

    [Fact]
    public async Task AddCartItemAsync_RejectsQuantityAboveAvailableStock()
    {
        await using var context = CreateContext();
        var service = CreateService(context, StockResponse(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddCartItemAsync(
                42,
                new AddCartItemRequest { ProductId = 7, Quantity = 2 },
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmPaymentAsync_ConfirmsPendingOrderIdempotently()
    {
        await using var context = CreateContext();
        context.Orders.Add(new CustomerOrder
        {
            UserId = 42,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 159.98m,
            Status = "Pending",
            ReservationId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, "{}");
        var request = new ConfirmPaymentRequest
        {
            OrderId = 1,
            UserId = 42,
            Amount = 159.98m,
            TransactionId = "mock-transaction-7",
            IdempotencyKey = "checkout-request-1"
        };

        Assert.True(await service.PreparePaymentAsync(request, CancellationToken.None));
        Assert.Equal("PaymentProcessing", (await context.Orders.SingleAsync()).Status);
        Assert.False(await service.CancelOrderAsync(42, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreparePaymentAsync(
                new PaymentReservationRequest
                {
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    Amount = request.Amount,
                    IdempotencyKey = "different-payment-attempt"
                },
                CancellationToken.None));
        Assert.True(await service.ConfirmPaymentAsync(request, CancellationToken.None));
        Assert.True(await service.ConfirmPaymentAsync(request, CancellationToken.None));
        var order = await context.Orders.SingleAsync();
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal("mock-transaction-7", order.PaymentTransactionId);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_RejectsMismatchedAmount()
    {
        await using var context = CreateContext();
        context.Orders.Add(new CustomerOrder
        {
            UserId = 42,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 159.98m,
            Status = "Pending",
            ReservationId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(
                new ConfirmPaymentRequest
                {
                    OrderId = 1,
                    UserId = 42,
                    Amount = 1,
                    TransactionId = "mock-transaction-7",
                    IdempotencyKey = "checkout-request-1"
                },
                CancellationToken.None));
        Assert.Equal("Pending", (await context.Orders.SingleAsync()).Status);
    }

    private static OrderManagementService CreateService(
        OrderDbContext context,
        string productJson)
    {
        return CreateService(context, productJson, new ProductResponseHandler("{}"));
    }

    private static OrderManagementService CreateService(
        OrderDbContext context,
        string productJson,
        HttpMessageHandler reservationHandler)
    {
        var client = new ProductCatalogClient(
            new HttpClient(new ProductResponseHandler(productJson))
            {
                BaseAddress = new Uri("http://product-service/")
            });
        return new OrderManagementService(
            context,
            client,
            new InventoryReservationClient(
                new HttpClient(reservationHandler)
                {
                    BaseAddress = new Uri("http://product-service/")
                },
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ProductService:InternalServiceKey"] = "internal-test-key"
                    })
                    .Build()),
            NullLogger<OrderManagementService>.Instance);
    }

    private static OrderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OrderDbContext(options);
    }

    private static string StockResponse(int quantity)
    {
        return $$"""
            {
              "id": 7,
              "name": "Trail Shoes",
              "price": 79.99,
              "imageUrl": "trail-shoes.png",
              "availableStock": {{quantity}},
              "isActive": true
            }
            """;
    }

    private sealed class AmbiguousReservationHandler : HttpMessageHandler
    {
        public bool ReservationExists { get; private set; }

        public bool ReleaseAttempted { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                ReservationExists = true;
                throw new HttpRequestException("The reservation response was lost.");
            }

            if (request.Method == HttpMethod.Delete)
            {
                ReleaseAttempted = true;
                ReservationExists = false;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            throw new InvalidOperationException($"Unexpected request method {request.Method}.");
        }
    }

    private sealed class ProductResponseHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
