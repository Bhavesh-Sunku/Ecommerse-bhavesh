using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.ProductService.Controllers;

[ApiController]
[Route("internal/inventory/reservations")]
public class InventoryReservationsController(
    InventoryReservationService reservationService,
    InternalServiceKeyValidator keyValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Reserve(
        InventoryReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!keyValidator.IsValid(Request.Headers["X-Internal-Service-Key"]))
        {
            return Unauthorized();
        }

        await reservationService.ReserveAsync(request, cancellationToken);
        return Ok();
    }

    [HttpDelete("{reservationId:guid}")]
    public async Task<IActionResult> Release(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (!keyValidator.IsValid(Request.Headers["X-Internal-Service-Key"]))
        {
            return Unauthorized();
        }

        await reservationService.ReleaseAsync(reservationId, cancellationToken);
        return NoContent();
    }
}
