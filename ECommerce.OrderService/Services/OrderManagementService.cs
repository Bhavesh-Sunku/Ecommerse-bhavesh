using System.Data;
using ECommerce.OrderService.Context;
using ECommerce.OrderService.Contracts;
using ECommerce.OrderService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.OrderService.Services;

public class OrderManagementService(
    OrderDbContext context,
    ProductCatalogClient productCatalog,
    InventoryReservationClient inventoryReservation,
    ILogger<OrderManagementService> logger)
{
    public async Task<CartResponse> GetCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        return await MapCartAsync(cart, cancellationToken);
    }

    public async Task<CartResponse> AddCartItemAsync(
        int userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var product = await GetAvailableProductAsync(request.ProductId, cancellationToken);
        if (request.Quantity > product.AvailableStock)
        {
            throw new InvalidOperationException("Requested quantity exceeds available stock.");
        }

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var item = cart.Items.FirstOrDefault(line => line.ProductId == request.ProductId);
        if (item is null)
        {
            cart.Items.Add(new ShoppingCartItem
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity
            });
        }
        else
        {
            var newQuantity = item.Quantity + request.Quantity;
            if (newQuantity > product.AvailableStock)
            {
                throw new InvalidOperationException("Requested quantity exceeds available stock.");
            }

            item.Quantity = newQuantity;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await MapCartAsync(cart, cancellationToken);
    }

    public async Task<CartResponse?> UpdateCartItemAsync(
        int userId,
        int itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var cartItem = cart?.Items.FirstOrDefault(item => item.Id == itemId);
        if (cartItem is null)
        {
            return null;
        }

        var product = await GetAvailableProductAsync(cartItem.ProductId, cancellationToken);
        if (request.Quantity > product.AvailableStock)
        {
            throw new InvalidOperationException("Requested quantity exceeds available stock.");
        }

        cartItem.Quantity = request.Quantity;
        await context.SaveChangesAsync(cancellationToken);
        return await MapCartAsync(cart!, cancellationToken);
    }

    public async Task<bool> RemoveCartItemAsync(
        int userId,
        int itemId,
        CancellationToken cancellationToken)
    {
        var cart = await context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var item = cart?.Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
        {
            return false;
        }

        context.CartItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ClearCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (cart is null)
        {
            return;
        }

        context.CartItems.RemoveRange(cart.Items);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderSummaryResponse> PlaceOrderAsync(
        int userId,
        PlaceOrderRequest request,
        string customerName,
        CancellationToken cancellationToken)
    {
        var initialCart = await context.Carts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);
        if (initialCart is null || initialCart.Items.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        var products = new Dictionary<int, ProductSummary>();
        foreach (var item in initialCart.Items)
        {
            var product = await GetAvailableProductAsync(item.ProductId, cancellationToken);
            if (item.Quantity > product.AvailableStock)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for product {item.ProductId}.");
            }

            products.Add(item.ProductId, product);
        }

        var reservationId = Guid.NewGuid();
        try
        {
            await inventoryReservation.ReserveAsync(
                reservationId,
                initialCart.Items
                    .Select(item => (item.ProductId, item.Quantity))
                    .ToArray(),
                cancellationToken);

            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var cart = await context.Carts
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (cart is null || !SameCartSnapshot(initialCart.Items, cart.Items))
            {
                throw new InvalidOperationException(
                    "Cart changed while checkout was being prepared. Please retry.");
            }

            var order = new CustomerOrder
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                CustomerName = customerName,
                ShippingAddress = request.ShippingAddress.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                Status = "Pending",
                ReservationId = reservationId,
                Items = cart.Items.Select(item =>
                {
                    var product = products[item.ProductId];
                    return new CustomerOrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = product.Name,
                        ProductImageUrl = product.ImageUrl,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    };
                }).ToList()
            };
            order.TotalAmount = order.Items.Sum(item => item.UnitPrice * item.Quantity);

            context.Orders.Add(order);
            context.CartItems.RemoveRange(cart.Items);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Placed pending order {OrderId} for user {UserId}", order.Id, userId);
            return MapOrder(order);
        }
        catch
        {
            try
            {
                await inventoryReservation.ReleaseAsync(reservationId, CancellationToken.None);
            }
            catch (Exception releaseFailure)
            {
                logger.LogCritical(
                    releaseFailure,
                    "Could not release inventory reservation {ReservationId} after order failure",
                    reservationId);
            }

            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(
        int userId,
        int orderId,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .FirstOrDefaultAsync(
                item => item.Id == orderId && item.UserId == userId,
                cancellationToken);
        if (order is null || order.Status != "Pending" || order.ReservationId is null)
        {
            return false;
        }

        await inventoryReservation.ReleaseAsync(order.ReservationId.Value, cancellationToken);
        order.Status = "Cancelled";
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Cancelled order {OrderId} and released reservation {ReservationId}",
            orderId,
            order.ReservationId);
        return true;
    }

    public async Task<bool> PreparePaymentAsync(
        PaymentReservationRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await context.Orders
            .FirstOrDefaultAsync(item => item.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        EnsurePaymentMatchesOrder(order, request.UserId, request.Amount);
        if (order.Status == "PaymentProcessing" &&
            order.PaymentIdempotencyKey == request.IdempotencyKey)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (order.Status != "Pending")
        {
            throw new InvalidOperationException("The order is not awaiting payment.");
        }

        order.Status = "PaymentProcessing";
        order.PaymentIdempotencyKey = request.IdempotencyKey;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReleasePaymentAsync(
        PaymentReservationRequest request,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .FirstOrDefaultAsync(item => item.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        EnsurePaymentMatchesOrder(order, request.UserId, request.Amount);
        if (order.Status == "Pending" && order.PaymentIdempotencyKey is null)
        {
            return true;
        }

        if (order.Status != "PaymentProcessing" ||
            order.PaymentIdempotencyKey != request.IdempotencyKey)
        {
            throw new InvalidOperationException("The order payment reservation cannot be released.");
        }

        order.Status = "Pending";
        order.PaymentIdempotencyKey = null;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await context.Orders
            .FirstOrDefaultAsync(item => item.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        if (order.UserId != request.UserId || order.TotalAmount != request.Amount)
        {
            throw new InvalidOperationException("Payment details do not match the order.");
        }

        if (order.Status == "Confirmed" &&
            order.PaymentTransactionId == request.TransactionId &&
            order.PaymentIdempotencyKey == request.IdempotencyKey)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (order.Status != "PaymentProcessing" ||
            order.PaymentIdempotencyKey != request.IdempotencyKey ||
            order.ReservationId is null)
        {
            throw new InvalidOperationException("The order is not awaiting payment.");
        }

        order.Status = "Confirmed";
        order.PaymentTransactionId = request.TransactionId;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Confirmed payment transaction {TransactionId} for order {OrderId}",
            request.TransactionId,
            request.OrderId);
        return true;
    }

    public async Task<List<OrderSummaryResponse>> GetOrdersAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var orders = await context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.OrderDate)
            .ToListAsync(cancellationToken);
        return orders.Select(MapOrder).ToList();
    }

    public async Task<OrderSummaryResponse?> GetOrderAsync(
        int userId,
        int orderId,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.Id == orderId && item.UserId == userId,
                cancellationToken);
        return order is null ? null : MapOrder(order);
    }

    private async Task<ShoppingCart> GetOrCreateCartAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var cart = await context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = new ShoppingCart { UserId = userId };
        context.Carts.Add(cart);
        await context.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task<CartResponse> MapCartAsync(
        ShoppingCart cart,
        CancellationToken cancellationToken)
    {
        var lines = new List<CartLineResponse>();
        foreach (var item in cart.Items)
        {
            var product = await productCatalog.GetProductAsync(item.ProductId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Product {item.ProductId} no longer exists.");
            lines.Add(new CartLineResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                AvailableStock = product.AvailableStock,
                Subtotal = product.Price * item.Quantity
            });
        }

        return new CartResponse
        {
            Id = cart.Id,
            Items = lines,
            TotalAmount = lines.Sum(line => line.Subtotal)
        };
    }

    private async Task<ProductSummary> GetAvailableProductAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        var product = await productCatalog.GetProductAsync(productId, cancellationToken);
        if (product is null || !product.IsActive)
        {
            throw new InvalidOperationException("Product is unavailable.");
        }

        return product;
    }

    private static bool SameCartSnapshot(
        ICollection<ShoppingCartItem> expected,
        ICollection<ShoppingCartItem> actual)
    {
        return expected.Count == actual.Count &&
            expected.OrderBy(item => item.Id)
                .Zip(actual.OrderBy(item => item.Id))
                .All(pair =>
                    pair.First.Id == pair.Second.Id &&
                    pair.First.ProductId == pair.Second.ProductId &&
                    pair.First.Quantity == pair.Second.Quantity);
    }

    private static void EnsurePaymentMatchesOrder(
        CustomerOrder order,
        int userId,
        decimal amount)
    {
        if (order.UserId != userId || order.TotalAmount != amount)
        {
            throw new InvalidOperationException("Payment details do not match the order.");
        }
    }

    private static OrderSummaryResponse MapOrder(CustomerOrder order)
    {
        var items = order.Items.Select(item => new CartLineResponse
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            ProductImageUrl = item.ProductImageUrl,
            ImageUrl = item.ProductImageUrl,
            UnitPrice = item.UnitPrice,
            Quantity = item.Quantity,
            Subtotal = item.UnitPrice * item.Quantity
        }).ToList();

        return new OrderSummaryResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            Items = items
        };
    }
}
