using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(ProductCatalogService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProductPage>> GetProducts([FromQuery] ProductQuery query)
    {
        return Ok(await productService.GetCatalogAsync(query));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        return Ok(await productService.GetCategoriesAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id)
    {
        var product = await productService.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request)
    {
        var product = await productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProduct(
        int id,
        UpdateProductRequest request)
    {
        return await productService.UpdateAsync(id, request)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        return await productService.DeleteAsync(id)
            ? NoContent()
            : NotFound();
    }
}
