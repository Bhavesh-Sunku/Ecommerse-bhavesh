using ECommerce.Business.DTOs.Cart;
using ECommerce.Business.Interfaces;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;

namespace ECommerce.Business.Services;

public class CartService(
    ICartRepository cartRepository,
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository) : ICartService
{
    private readonly ICartRepository _cartRepository = cartRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IInventoryRepository _inventoryRepository = inventoryRepository;

    public async Task<CartResponseDto> GetAsync(int userId)
    {
        ValidateUserId(userId);
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        return cart == null ? new CartResponseDto() : Map(cart);
    }

    public async Task<CartResponseDto> AddItemAsync(int userId, AddCartItemDto dto)
    {
        ValidateUserId(userId);
        if (dto.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }

        var product = await _productRepository.GetByIdAsync(dto.ProductId)
            ?? throw new ArgumentException($"Product {dto.ProductId} does not exist.");
        if (!product.IsActive)
        {
            throw new InvalidOperationException("This product is not available.");
        }

        var inventory = await _inventoryRepository.GetByProductIdAsync(dto.ProductId);
        if (inventory == null || inventory.Quantity < dto.Quantity)
        {
            throw new InvalidOperationException("Insufficient stock for this product.");
        }

        var cart = await GetOrCreateCartAsync(userId);
        var item = cart.Items.FirstOrDefault(existing => existing.ProductId == dto.ProductId);
        var resultingQuantity = (item?.Quantity ?? 0) + dto.Quantity;
        if (resultingQuantity > inventory.Quantity)
        {
            throw new InvalidOperationException("Requested quantity exceeds available stock.");
        }

        if (item == null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                Product = product,
                Quantity = dto.Quantity
            });
            product.Inventory = inventory;
        }
        else
        {
            item.Quantity = resultingQuantity;
        }

        await _cartRepository.SaveChangesAsync();
        return Map(cart);
    }

    public async Task<CartResponseDto> UpdateItemAsync(int userId, int itemId, int quantity)
    {
        ValidateUserId(userId);
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }

        var cart = await GetRequiredCartAsync(userId);
        var item = cart.Items.FirstOrDefault(existing => existing.Id == itemId)
            ?? throw new KeyNotFoundException("Cart item was not found.");
        var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
        if (!item.Product.IsActive || inventory == null || quantity > inventory.Quantity)
        {
            throw new InvalidOperationException("Requested quantity exceeds available stock.");
        }

        item.Quantity = quantity;
        await _cartRepository.SaveChangesAsync();
        return Map(cart);
    }

    public async Task<CartResponseDto> RemoveItemAsync(int userId, int itemId)
    {
        ValidateUserId(userId);
        var cart = await GetRequiredCartAsync(userId);
        var item = cart.Items.FirstOrDefault(existing => existing.Id == itemId)
            ?? throw new KeyNotFoundException("Cart item was not found.");

        cart.Items.Remove(item);
        await _cartRepository.SaveChangesAsync();
        return Map(cart);
    }

    public async Task<CartResponseDto> ClearAsync(int userId)
    {
        ValidateUserId(userId);
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart == null)
        {
            return new CartResponseDto();
        }

        cart.Items.Clear();
        await _cartRepository.SaveChangesAsync();
        return Map(cart);
    }

    private async Task<Cart> GetOrCreateCartAsync(int userId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        return cart ?? await _cartRepository.AddAsync(new Cart { UserId = userId });
    }

    private async Task<Cart> GetRequiredCartAsync(int userId)
    {
        return await _cartRepository.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Cart was not found.");
    }

    private static void ValidateUserId(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Invalid user ID.");
        }
    }

    private static CartResponseDto Map(Cart cart)
    {
        return new CartResponseDto
        {
            Id = cart.Id,
            Items = cart.Items.Select(item => new CartItemResponseDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ImageUrl = item.Product.ImageUrl,
                UnitPrice = item.Product.Price,
                Quantity = item.Quantity,
                AvailableStock = item.Product.Inventory?.Quantity ?? 0
            }).ToList()
        };
    }
}
