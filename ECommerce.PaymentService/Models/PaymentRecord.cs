namespace ECommerce.Payments.Models;

public class PaymentRecord
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = PaymentStatuses.Pending;

    public string PaymentMethod { get; set; } = "mock-success";

    public string IdempotencyKey { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public bool OrderConfirmed { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Successful = "Successful";
    public const string Failed = "Failed";
}
