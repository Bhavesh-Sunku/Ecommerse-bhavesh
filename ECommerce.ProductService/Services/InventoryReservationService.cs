using System.Data;
using ECommerce.ProductService.Context;
using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Services;

public class InventoryReservationService(
    ProductDbContext context,
    ILogger<InventoryReservationService> logger)
{
    public async Task ReserveAsync(
        InventoryReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReservationId == Guid.Empty ||
            request.Items is null ||
            request.Items.Count == 0 ||
        request.Items.Any(item =>
            item is null ||
            item.ProductId <= 0 ||
            item.Quantity <= 0))
        {
            throw new ArgumentException("Reservation ID and valid items are required.");
        }

        var groupedItems = request.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => (long)item.Quantity)
            })
            .OrderBy(item => item.ProductId)
            .ToList();
        if (groupedItems.Any(item => item.Quantity > int.MaxValue))
        {
            throw new ArgumentException("Combined reservation quantity is too large.");
        }
        var requestedItems = groupedItems
            .Select(item => new
            {
                item.ProductId,
                Quantity = (int)item.Quantity
            })
            .ToList();

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.InventoryReservations
            .Where(item => item.ReservationId == request.ReservationId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            var matches = existing.Count == requestedItems.Count &&
                existing.All(item => item.Status == "Reserved" &&
                    requestedItems.Any(requested =>
                        requested.ProductId == item.ProductId &&
                        requested.Quantity == item.Quantity));
            if (!matches)
            {
                throw new InvalidOperationException(
                    "Reservation ID was already used with different or released items.");
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var inventoryItems = new List<(Inventory Inventory, int Quantity)>();
        foreach (var requested in requestedItems)
        {
            var inventory = await context.Inventories
                .FirstOrDefaultAsync(
                    item => item.ProductId == requested.ProductId,
                    cancellationToken);
            if (inventory is null || inventory.Quantity < requested.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for product {requested.ProductId}.");
            }

            inventoryItems.Add((inventory, requested.Quantity));
        }

        var now = DateTime.UtcNow;
        foreach (var (inventory, quantity) in inventoryItems)
        {
            inventory.Quantity -= quantity;
            inventory.UpdatedAt = now;
            context.InventoryReservations.Add(new InventoryReservation
            {
                ReservationId = request.ReservationId,
                ProductId = inventory.ProductId,
                Quantity = quantity,
                Status = "Reserved",
                CreatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Reserved inventory under reservation {ReservationId}",
            request.ReservationId);
    }

    public async Task<bool> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var reservations = await context.InventoryReservations
            .Where(item => item.ReservationId == reservationId)
            .ToListAsync(cancellationToken);
        if (reservations.Count == 0)
        {
            return false;
        }

        if (reservations.All(item => item.Status == "Released"))
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        foreach (var reservation in reservations.Where(item => item.Status == "Reserved"))
        {
            var inventory = await context.Inventories
                .FirstOrDefaultAsync(
                    item => item.ProductId == reservation.ProductId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Inventory for product {reservation.ProductId} no longer exists.");

            inventory.Quantity = checked(inventory.Quantity + reservation.Quantity);
            inventory.UpdatedAt = DateTime.UtcNow;
            reservation.Status = "Released";
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Released inventory reservation {ReservationId}",
            reservationId);
        return true;
    }
}
