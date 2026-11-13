using ECommerce.Business.DTOs.Cart;

namespace ECommerce.Business.Interfaces;

public interface ICartService
{
    Task<CartResponseDto> GetAsync(int userId);

    Task<CartResponseDto> AddItemAsync(int userId, AddCartItemDto dto);

    Task<CartResponseDto> UpdateItemAsync(int userId, int itemId, int quantity);

    Task<CartResponseDto> RemoveItemAsync(int userId, int itemId);

    Task<CartResponseDto> ClearAsync(int userId);
}
