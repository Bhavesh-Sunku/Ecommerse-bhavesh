using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(InventoryManagementService inventoryService) : ControllerBase
{
    [HttpGet("product/{productId:int}")]
    public async Task<ActionResult<InventoryResponse>> GetInventory(int productId)
    {
        var inventory = await inventoryService.GetByProductIdAsync(productId);
        return inventory is null ? NotFound() : Ok(inventory);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InventoryResponse>> CreateInventory(
        CreateInventoryRequest request)
    {
        var inventory = await inventoryService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetInventory),
            new { productId = inventory.ProductId },
            inventory);
    }

    [HttpPut("product/{productId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateInventory(
        int productId,
        UpdateInventoryRequest request)
    {
        return await inventoryService.UpdateAsync(productId, request)
            ? NoContent()
            : NotFound();
    }
}
