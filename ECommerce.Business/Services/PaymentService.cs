using ECommerce.Business.DTOs.Payments;
using ECommerce.Business.Interfaces;

namespace ECommerce.Business.Services;

public class PaymentService : IPaymentService
{
    public Task<PaymentResponseDto> CheckoutAsync(
        int orderId,
        int userId,
        CheckoutDto dto)
    {
        return Task.FromException<PaymentResponseDto>(
            new NotSupportedException(
                "Payment processing is not configured. No payment was taken."));
    }
}