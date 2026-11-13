namespace ECommerce.Business.DTOs.Products;

public class BulkImportRowErrorDto
{
    public int RowNumber { get; set; }

    public string Error { get; set; } = string.Empty;
}
