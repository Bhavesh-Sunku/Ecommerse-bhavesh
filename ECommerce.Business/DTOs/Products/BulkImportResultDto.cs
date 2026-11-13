using System.Collections.Generic;

namespace ECommerce.Business.DTOs.Products;

public class BulkImportResultDto
{
    public int Created { get; set; }

    public int Updated { get; set; }

    public List<BulkImportRowErrorDto> Errors { get; set; } =
        new List<BulkImportRowErrorDto>();
}
