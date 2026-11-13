using System.Security.Claims;
using ECommerce.Business.DTOs.Cart;
using ECommerce.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController(ICartService cartService) : ControllerBase
{
    private readonly ICartService _cartService = cartService;

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        return Ok(await _cartService.GetAsync(GetUserId()));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddCartItemDto dto)
    {
        return Ok(await _cartService.AddItemAsync(GetUserId(), dto));
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int itemId, UpdateCartItemDto dto)
    {
        return Ok(await _cartService.UpdateItemAsync(GetUserId(), itemId, dto.Quantity));
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId)
    {
        return Ok(await _cartService.RemoveItemAsync(GetUserId(), itemId));
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        return Ok(await _cartService.ClearAsync(GetUserId()));
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user identity.");
        }

        return userId;
    }
}
