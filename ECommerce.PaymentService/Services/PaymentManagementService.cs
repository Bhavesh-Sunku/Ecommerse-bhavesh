using ECommerce.Payments.Context;
using ECommerce.Payments.Contracts;
using ECommerce.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Payments.Services;

public class PaymentManagementService(
    PaymentDbContext context,
    IOrderClient orderClient,
    IMockPaymentProvider paymentProvider,
    ILogger<PaymentManagementService> logger)
{
    public async Task<PaymentResponse> CreateAsync(
        int userId,
        string bearerToken,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await context.Payments
            .FirstOrDefaultAsync(
                payment => payment.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.UserId != userId || existing.OrderId != request.OrderId)
            {
                throw new InvalidOperationException(
                    "The idempotency key was already used for a different payment.");
            }

            if (existing.PaymentMethod != request.PaymentMethod)
            {
                throw new InvalidOperationException(
                    "The idempotency key cannot be reused with a different payment method.");
            }

            if (existing.Status == PaymentStatuses.Successful &&
                existing.TransactionId is not null)
            {
                return await ConfirmExistingPaymentAsync(existing, cancellationToken);
            }

            if (existing.Status == PaymentStatuses.Failed)
            {
                var failedOrder = await orderClient.GetOrderAsync(
                    existing.OrderId,
                    bearerToken,
                    cancellationToken);
                if (failedOrder is not null &&
                    (failedOrder.Status == "PaymentProcessing" ||
                     failedOrder.Status == "Pending"))
                {
                    try
                    {
                        await orderClient.ReleasePaymentAsync(
                            failedOrder,
                            existing.IdempotencyKey,
                            cancellationToken);
                    }
                    catch (OrderServiceException exception)
                        when (exception.StatusCode == StatusCodes.Status409Conflict)
                    {
                        logger.LogInformation(
                            "Payment attempt {PaymentId} no longer owns order {OrderId}",
                            existing.Id,
                            existing.OrderId);
                    }
                }

                return Map(existing);
            }
        }

        var order = await orderClient.GetOrderAsync(
            request.OrderId,
            bearerToken,
            cancellationToken);
        if (order is null)
        {
            throw new KeyNotFoundException("Order was not found.");
        }

        if (order.UserId != userId)
        {
            throw new UnauthorizedAccessException("Order does not belong to the authenticated user.");
        }

        if ((order.Status != "Pending" &&
             !(existing is not null && order.Status == "PaymentProcessing")) ||
            order.TotalAmount <= 0)
        {
            throw new InvalidOperationException("Order is not awaiting payment.");
        }

        var payment = existing ?? new PaymentRecord
        {
            OrderId = order.Id,
            UserId = userId,
            Amount = order.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            IdempotencyKey = request.IdempotencyKey,
            Status = PaymentStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        if (existing is null)
        {
            context.Payments.Add(payment);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                context.Entry(payment).State = EntityState.Detached;
                existing = await context.Payments.FirstOrDefaultAsync(
                    item => item.IdempotencyKey == request.IdempotencyKey,
                    cancellationToken);
                if (existing is null)
                {
                    throw;
                }

                if (existing.UserId != userId ||
                    existing.OrderId != request.OrderId ||
                    existing.PaymentMethod != request.PaymentMethod)
                {
                    throw new InvalidOperationException(
                        "The idempotency key was already used for a different payment request.");
                }

                if (existing.Status == PaymentStatuses.Successful &&
                    existing.TransactionId is not null)
                {
                    return await ConfirmExistingPaymentAsync(existing, cancellationToken);
                }

                if (existing.Status == PaymentStatuses.Failed)
                {
                    return Map(existing);
                }

                payment = existing;
            }
        }

        try
        {
            await orderClient.PreparePaymentAsync(
                order,
                payment.IdempotencyKey,
                cancellationToken);
        }
        catch (OrderServiceException exception)
            when (exception.StatusCode == StatusCodes.Status409Conflict)
        {
            payment.Status = PaymentStatuses.Failed;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(
                "Another payment attempt is in progress or the order is no longer payable.");
        }

        var providerResult = await paymentProvider.ProcessAsync(
            order.Id,
            order.TotalAmount,
            request.PaymentMethod,
            cancellationToken);
        if (!providerResult.Succeeded)
        {
            payment.Status = PaymentStatuses.Failed;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await orderClient.ReleasePaymentAsync(
                order,
                payment.IdempotencyKey,
                cancellationToken);
            return Map(payment);
        }

        if (string.IsNullOrWhiteSpace(providerResult.TransactionId))
        {
            throw new InvalidOperationException(
                "Payment provider reported success without a transaction identifier.");
        }

        payment.Status = PaymentStatuses.Successful;
        payment.TransactionId = providerResult.TransactionId;
        payment.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await orderClient.ConfirmPaymentAsync(
            order,
            payment.TransactionId,
            payment.IdempotencyKey,
            cancellationToken);
        payment.OrderConfirmed = true;
        payment.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Recorded successful mock payment {PaymentId} for order {OrderId}",
            payment.Id,
            order.Id);
        return Map(payment);
    }

    public async Task<PaymentResponse?> GetByOrderAsync(
        int userId,
        int orderId,
        CancellationToken cancellationToken)
    {
        var payment = await context.Payments
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.OrderId == orderId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return payment is null
            ? null
            : Map(payment);
    }

    private static PaymentResponse Map(PaymentRecord payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            Status = payment.Status,
            TransactionId = payment.TransactionId,
            CreatedAtUtc = payment.CreatedAtUtc,
            OrderConfirmed = payment.OrderConfirmed
        };
    }

    private async Task<PaymentResponse> ConfirmExistingPaymentAsync(
        PaymentRecord payment,
        CancellationToken cancellationToken)
    {
        await orderClient.ConfirmPaymentAsync(
            new OrderSnapshot
            {
                Id = payment.OrderId,
                UserId = payment.UserId,
                TotalAmount = payment.Amount
            },
            payment.TransactionId!,
            payment.IdempotencyKey,
            cancellationToken);
        if (!payment.OrderConfirmed)
        {
            payment.OrderConfirmed = true;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return Map(payment);
    }
}
