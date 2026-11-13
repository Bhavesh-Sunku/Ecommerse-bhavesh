using System.Security.Claims;
using ECommerce.Payments.Contracts;
using ECommerce.Payments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Payments.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(PaymentManagementService paymentService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<PaymentResponse>> Create(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await paymentService.CreateAsync(
            GetUserId(),
            Request.Headers.Authorization.ToString(),
            request,
            cancellationToken);
        return response.Status == "Failed"
            ? StatusCode(StatusCodes.Status402PaymentRequired, response)
            : Ok(response);
    }

    [HttpGet("orders/{orderId:int}")]
    public async Task<ActionResult<PaymentResponse>> GetByOrder(
        int orderId,
        CancellationToken cancellationToken)
    {
        var response = await paymentService.GetByOrderAsync(
            GetUserId(),
            orderId,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User ID claim is missing.");
    }
}
