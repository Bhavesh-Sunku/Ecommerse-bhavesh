using ECommerce.DataAccess.Context;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.DataAccess.Repositories.Implementations;

public class CartRepository(EcommerceDbContext context) : ICartRepository
{
    private readonly EcommerceDbContext _context = context;

    public Task<Cart?> GetByUserIdAsync(int userId)
    {
        return _context.Carts
            .Include(cart => cart.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Inventory)
            .FirstOrDefaultAsync(cart => cart.UserId == userId);
    }

    public async Task<Cart> AddAsync(Cart cart)
    {
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();
        return cart;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
