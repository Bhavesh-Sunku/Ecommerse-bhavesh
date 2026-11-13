using System.Security.Claims;
using ECommerce.OrderService.Contracts;
using ECommerce.OrderService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.OrderService.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController(OrderManagementService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartResponse>> GetCart(CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetCartAsync(GetUserId(), cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.AddCartItemAsync(
            GetUserId(),
            request,
            cancellationToken));
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        int itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await orderService.UpdateCartItemAsync(
            GetUserId(),
            itemId,
            request,
            cancellationToken);
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId, CancellationToken cancellationToken)
    {
        return await orderService.RemoveCartItemAsync(
            GetUserId(),
            itemId,
            cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        await orderService.ClearCartAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User ID claim is missing.");
    }
}
