using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Domain.Entities;

namespace ECommerce.DataAccess.Repositories.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);

    Task<Order> CreateAtomicallyAsync(
        Order order,
        IReadOnlyDictionary<int, int> quantities,
        int? cartId,
        int expectedCartItemCount);

    Task<bool> CancelPendingWithRestockAsync(int orderId, int userId);

    Task<Order?> GetByIdAsync(int id);

    Task<List<Order>> GetByUserIdAsync(int userId);

    Task UpdateAsync(Order order);
    Task<List<Order>> GetPendingOrdersOlderThanAsync(DateTime cutoff);

}