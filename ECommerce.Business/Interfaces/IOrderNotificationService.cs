namespace ECommerce.Business.Interfaces;

public interface IOrderNotificationService
{
    Task NotifyOrderCreatedAsync(
        int userId,
        int orderId,
        decimal totalAmount);

    Task NotifyOrderCancelledAsync(
        int userId,
        int orderId);
}