using System.Security.Cryptography;
using System.Text;

namespace ECommerce.ProductService.Services;

public class InternalServiceKeyValidator(IConfiguration configuration)
{
    public bool IsValid(string? candidate)
    {
        var configuredKey = configuration["InternalServices:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(configuredKey);
        var actualBytes = Encoding.UTF8.GetBytes(candidate);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
