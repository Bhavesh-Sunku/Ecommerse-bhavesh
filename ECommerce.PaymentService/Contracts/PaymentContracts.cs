using System.ComponentModel.DataAnnotations;

namespace ECommerce.Payments.Contracts;

public class CreatePaymentRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(mock-success|mock-decline)$")]
    public string PaymentMethod { get; set; } = "mock-success";
}

public class PaymentResponse
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public bool OrderConfirmed { get; set; }
}

public class OrderSnapshot
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class MockProviderResult
{
    public bool Succeeded { get; init; }

    public string? TransactionId { get; init; }

    public string? FailureReason { get; init; }
}
