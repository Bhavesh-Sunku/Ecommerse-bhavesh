using ECommerce.Domain.Entities;

namespace ECommerce.DataAccess.Repositories.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(int userId);

    Task<Cart> AddAsync(Cart cart);

    Task SaveChangesAsync();
}
