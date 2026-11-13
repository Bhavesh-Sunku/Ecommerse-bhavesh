using System.Data;
using ECommerce.DataAccess.Context;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.DataAccess.Repositories.Implementations;

public class OrderRepository : IOrderRepository
{
    private readonly EcommerceDbContext _context;

    public OrderRepository(EcommerceDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // Create Order
    // ==========================================

    public async Task<Order> AddAsync(Order order)
    {
        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        return order;
    }

    public async Task<Order> CreateAtomicallyAsync(
        Order order,
        IReadOnlyDictionary<int, int> quantities,
        int? cartId,
        int expectedCartItemCount)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        if (cartId.HasValue)
        {
            var cartItems = await _context.CartItems
                .AsNoTracking()
                .Where(item => item.CartId == cartId.Value)
                .ToListAsync();

            if (cartItems.Count != expectedCartItemCount ||
                cartItems.Any(item =>
                    !quantities.TryGetValue(item.ProductId, out var quantity) ||
                    quantity != item.Quantity))
            {
                throw new InvalidOperationException(
                    "The cart changed while the order was being placed. Please review it and try again.");
            }

            var deletedItems = await _context.CartItems
                .Where(item => item.CartId == cartId.Value)
                .ExecuteDeleteAsync();

            if (deletedItems != expectedCartItemCount)
            {
                throw new InvalidOperationException(
                    "The cart changed while the order was being placed. Please review it and try again.");
            }
        }

        foreach (var (productId, quantity) in quantities)
        {
            var updatedRows = await _context.Inventories
                .Where(inventory =>
                    inventory.ProductId == productId &&
                    inventory.Quantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(inventory => inventory.Quantity, inventory => inventory.Quantity - quantity)
                    .SetProperty(inventory => inventory.UpdatedAt, DateTime.UtcNow));

            if (updatedRows != 1)
            {
                throw new InvalidOperationException("Insufficient stock to place this order.");
            }
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return order;
    }

    public async Task<bool> CancelPendingWithRestockAsync(int orderId, int userId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var order = await _context.Orders
            .Include(existing => existing.OrderItems)
            .FirstOrDefaultAsync(existing =>
                existing.Id == orderId &&
                existing.UserId == userId &&
                existing.Status == OrderStatus.Pending);

        if (order == null)
        {
            return false;
        }

        var updatedOrders = await _context.Orders
            .Where(existing =>
                existing.Id == orderId &&
                existing.UserId == userId &&
                existing.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(existing => existing.Status, OrderStatus.Cancelled));

        if (updatedOrders != 1)
        {
            return false;
        }

        foreach (var itemGroup in order.OrderItems.GroupBy(item => item.ProductId))
        {
            var quantity = itemGroup.Sum(item => item.Quantity);
            var updatedInventories = await _context.Inventories
                .Where(inventory => inventory.ProductId == itemGroup.Key)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(inventory => inventory.Quantity, inventory => inventory.Quantity + quantity)
                    .SetProperty(inventory => inventory.UpdatedAt, DateTime.UtcNow));

            if (updatedInventories != 1)
            {
                throw new InvalidOperationException(
                    $"Inventory is missing for product {itemGroup.Key}.");
            }
        }

        await transaction.CommitAsync();
        return true;
    }

    // ==========================================
    // Get Order By ID
    // ==========================================

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    // ==========================================
    // Get Orders By User
    // ==========================================

    public async Task<List<Order>> GetByUserIdAsync(
        int userId)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    // ==========================================
    // Update Order
    // ==========================================

    public async Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);

        await _context.SaveChangesAsync();
    }

    // ==========================================
    // Get Old Pending Orders
    // ==========================================

    public async Task<List<Order>> GetPendingOrdersOlderThanAsync(
        DateTime cutoff)
    {
        return await _context.Orders
            .Where(o =>
                o.Status == OrderStatus.Pending &&
                o.OrderDate < cutoff)
            .ToListAsync();
    }
}