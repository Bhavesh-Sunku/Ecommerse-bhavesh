using System.Security.Claims;
using ECommerce.Business.DTOs.Orders;
using ECommerce.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController(IOrderService orderService, ILogger<OrdersController> logger) : ControllerBase
{
    private readonly IOrderService _orderService = orderService;
    private readonly ILogger<OrdersController> _logger = logger;


    [HttpPost("items")]
    public async Task<IActionResult> CreateOrder(
        CreateOrderDto dto)
    {
        var userId = GetUserId();

        var order =
            await _orderService.CreateAsync(
                userId,
                dto);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            order);
    }

    [HttpPost]
    [HttpPost("from-cart")]
    public async Task<IActionResult> CreateOrderFromCart(
        PlaceOrderFromCartDto dto)
    {
        var order = await _orderService.CreateFromCartAsync(GetUserId(), dto);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            order);
    }

    [HttpGet]
    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();

        var orders =
            await _orderService.GetMyOrdersAsync(
                userId);

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var userId = GetUserId();

        var order =
            await _orderService.GetByIdAsync(
                id,
                userId);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = GetUserId();

        var cancelled =
            await _orderService.CancelAsync(
                id,
                userId);

        if (!cancelled)
        {
            return NotFound();
        }

        return NoContent();
    }

    private int GetUserId()
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(
                userIdClaim,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "Invalid user identity.");
        }

        return userId;
    }
}