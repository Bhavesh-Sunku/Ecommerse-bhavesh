using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ECommerce.Business.Interfaces;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/vendor/products")]
[Authorize(Roles = "Vendor")]
public class VendorProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public VendorProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpPost("bulk")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkAdd([FromForm] Models.VendorProductUploadDto dto)
    {
        var file = dto.File;

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        using var stream = file.OpenReadStream();
        var result = await _productService.ImportFromExcelAsync(stream, file.FileName);

        return Ok(result);
    }
}
