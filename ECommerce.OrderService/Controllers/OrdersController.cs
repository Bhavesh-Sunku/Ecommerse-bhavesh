using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECommerce.OrderService.Contracts;
using ECommerce.OrderService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ECommerce.OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(
    OrderManagementService orderService,
    IConfiguration configuration,
    ILogger<OrdersController> logger) : ControllerBase
{
    private const string PaymentKeyHeader = "X-Payment-Service-Key";

    [AllowAnonymous]
    [HttpPost("/internal/payments/prepare")]
    public async Task<IActionResult> PreparePayment(
        PaymentReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPaymentServiceAuthorized())
        {
            return PaymentServiceUnavailableOrUnauthorized();
        }

        try
        {
            return await orderService.PreparePaymentAsync(request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Rejected payment preparation for order {OrderId}", request.OrderId);
            return Conflict(new ProblemDetails
            {
                Title = "Order cannot be reserved for this payment.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("/internal/payments/release")]
    public async Task<IActionResult> ReleasePayment(
        PaymentReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPaymentServiceAuthorized())
        {
            return PaymentServiceUnavailableOrUnauthorized();
        }

        try
        {
            return await orderService.ReleasePaymentAsync(request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Rejected payment release for order {OrderId}", request.OrderId);
            return Conflict(new ProblemDetails
            {
                Title = "Order payment reservation cannot be released.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("/internal/payments/confirm")]
    public async Task<IActionResult> ConfirmPayment(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPaymentServiceAuthorized())
        {
            return PaymentServiceUnavailableOrUnauthorized();
        }

        try
        {
            return await orderService.ConfirmPaymentAsync(request, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Rejected payment confirmation for order {OrderId}",
                request.OrderId);
            return Conflict(new ProblemDetails
            {
                Title = "Order cannot be confirmed with these payment details.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("from-cart")]
    public async Task<ActionResult<OrderSummaryResponse>> PlaceFromCart(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var order = await orderService.PlaceOrderAsync(
            GetUserId(),
            request,
            userName,
            cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet]
    [HttpGet("my-orders")]
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetMyOrders(
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetOrdersAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderSummaryResponse>> GetOrder(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(
            GetUserId(),
            id,
            cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelOrder(
        int id,
        CancellationToken cancellationToken)
    {
        return await orderService.CancelOrderAsync(
            GetUserId(),
            id,
            cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User ID claim is missing.");
    }

    private bool IsPaymentServiceAuthorized()
    {
        var expectedKey = configuration["PaymentService:InternalServiceKey"];
        return !string.IsNullOrWhiteSpace(expectedKey) &&
            Encoding.UTF8.GetByteCount(expectedKey) >= 32 &&
            Request.Headers.TryGetValue(PaymentKeyHeader, out StringValues suppliedKey) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedKey.ToString()),
                Encoding.UTF8.GetBytes(expectedKey));
    }

    private IActionResult PaymentServiceUnavailableOrUnauthorized()
    {
        var expectedKey = configuration["PaymentService:InternalServiceKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            Encoding.UTF8.GetByteCount(expectedKey) < 32)
        {
            logger.LogError("Payment service operations are disabled because their service key is not configured.");
            return Problem(
                title: "Payment service integration is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Unauthorized();
    }
}
