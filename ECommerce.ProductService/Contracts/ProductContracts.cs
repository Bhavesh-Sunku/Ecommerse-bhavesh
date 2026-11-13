using System.ComponentModel.DataAnnotations;

namespace ECommerce.ProductService.Contracts;

public class ProductQuery
{
    public string? Search { get; set; }

    public string? Category { get; set; }

    public string SortBy { get; set; } = "newest";

    public bool SortDescending { get; set; } = true;

    public bool IncludeOutOfStock { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

public class ProductPage
{
    public List<ProductResponse> Products { get; set; } = [];

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}

public class ProductResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Category { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public int AvailableStock { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateProductRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}

public class UpdateProductRequest : CreateProductRequest
{
    public bool IsActive { get; set; }
}

public class InventoryResponse
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class CreateInventoryRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public class UpdateInventoryRequest
{
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public class InventoryReservationRequest
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    [MinLength(1)]
    public List<InventoryReservationItemRequest> Items { get; set; } = [];
}

public class InventoryReservationItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
