using Microsoft.AspNetCore.Http;

namespace ECommerce.API.Models;

public class VendorProductUploadDto
{
    public IFormFile? File { get; set; }
}
