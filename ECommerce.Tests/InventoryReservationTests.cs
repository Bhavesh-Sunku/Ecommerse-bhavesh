using ECommerce.ProductService.Context;
using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Models;
using ECommerce.ProductService.Services;
using ECommerce.ProductService.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests;

public class InventoryReservationTests
{
    [Fact]
    public async Task ReserveAndReleaseAsync_ChangesStockAndIsIdempotent()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 17,
            Name = "Demo",
            Price = 10,
            Category = "Test",
            Inventory = new Inventory { Quantity = 5, UpdatedAt = DateTime.UtcNow }
        });
        await context.SaveChangesAsync();

        var service = new InventoryReservationService(
            context,
            NullLogger<InventoryReservationService>.Instance);
        var request = new InventoryReservationRequest
        {
            ReservationId = Guid.NewGuid(),
            Items = [new InventoryReservationItemRequest { ProductId = 17, Quantity = 3 }]
        };

        await service.ReserveAsync(request, CancellationToken.None);
        await service.ReserveAsync(request, CancellationToken.None);
        Assert.Equal(2, (await context.Inventories.SingleAsync()).Quantity);

        Assert.True(await service.ReleaseAsync(request.ReservationId, CancellationToken.None));
        Assert.True(await service.ReleaseAsync(request.ReservationId, CancellationToken.None));
        Assert.Equal(5, (await context.Inventories.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ReserveAsync_RejectsInsufficientStockWithoutChangingInventory()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 17,
            Name = "Demo",
            Price = 10,
            Category = "Test",
            Inventory = new Inventory { Quantity = 1, UpdatedAt = DateTime.UtcNow }
        });
        await context.SaveChangesAsync();

        var service = new InventoryReservationService(
            context,
            NullLogger<InventoryReservationService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReserveAsync(new InventoryReservationRequest
            {
                ReservationId = Guid.NewGuid(),
                Items = [new InventoryReservationItemRequest { ProductId = 17, Quantity = 2 }]
            }, CancellationToken.None));

        Assert.Equal(1, (await context.Inventories.SingleAsync()).Quantity);
        Assert.Empty(await context.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task ReserveAsync_RejectsNegativeQuantityWithoutChangingInventory()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 17,
            Name = "Demo",
            Price = 10,
            Category = "Test",
            Inventory = new Inventory { Quantity = 5, UpdatedAt = DateTime.UtcNow }
        });
        await context.SaveChangesAsync();

        var service = new InventoryReservationService(
            context,
            NullLogger<InventoryReservationService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReserveAsync(new InventoryReservationRequest
            {
                ReservationId = Guid.NewGuid(),
                Items = [new InventoryReservationItemRequest { ProductId = 17, Quantity = -2 }]
            }, CancellationToken.None));

        Assert.Equal(5, (await context.Inventories.SingleAsync()).Quantity);
        Assert.Empty(await context.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task Release_UnknownReservationReturnsNoContent()
    {
        await using var context = CreateContext();
        var reservationService = new InventoryReservationService(
            context,
            NullLogger<InventoryReservationService>.Instance);
        const string serviceKey = "a-test-internal-key-that-is-long-enough";
        var validator = new InternalServiceKeyValidator(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InternalServices:ApiKey"] = serviceKey
                })
                .Build());
        var controller = new InventoryReservationsController(reservationService, validator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Headers["X-Internal-Service-Key"] = serviceKey;

        var result = await controller.Release(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    private static ProductDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ProductDbContext(options);
    }
}
