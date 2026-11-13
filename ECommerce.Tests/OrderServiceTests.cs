using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Business.DTOs.Orders;
using ECommerce.Business.Interfaces;
using ECommerce.Business.Services;
using LegacyOrderService = ECommerce.Business.Services.OrderService;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Moq;

namespace ECommerce.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsException()
    {
        // Arrange
        var product = new Product
        {
            Id = 1,
            Name = "Laptop",
            Price = 50000,
            IsActive = true
        };

        var inventory = new Inventory
        {
            ProductId = 1,
            Quantity = 2
        };

        var productRepository =
            new Mock<IProductRepository>();

        productRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(product);

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        inventoryRepository
            .Setup(x => x.GetByProductIdAsync(1))
            .ReturnsAsync(inventory);

        var orderRepository =
            new Mock<IOrderRepository>();

        var service = new LegacyOrderService(
            orderRepository.Object,
            productRepository.Object,
            inventoryRepository.Object,
            new Mock<ICartRepository>().Object,
            new Mock<IProductService>().Object);

        var dto = new CreateOrderDto
        {
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto
                {
                    ProductId = 1,
                    Quantity = 5
                }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(1, dto));
    }

    [Fact]
    public async Task CreateFromCartAsync_CreatesOrderWithShippingAndCartQuantities()
    {
        var product = new Product
        {
            Id = 7,
            Name = "Headphones",
            Price = 49.99m,
            IsActive = true
        };
        var cart = new Cart
        {
            Id = 11,
            UserId = 12,
            Items =
            [
                new CartItem
                {
                    Id = 3,
                    ProductId = product.Id,
                    Quantity = 2,
                    Product = product
                }
            ]
        };
        var savedOrder = new Order
        {
            Id = 20,
            UserId = 12,
            TotalAmount = 99.98m,
            Status = ECommerce.Domain.Enums.OrderStatus.Pending,
            CustomerName = "Customer",
            ShippingAddress = "123 Main Street",
            PhoneNumber = "1234567890",
            OrderItems =
            [
                new OrderItem
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 2,
                    UnitPrice = product.Price
                }
            ]
        };

        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByIdAsync(product.Id))
            .ReturnsAsync(product);

        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repository => repository.GetByProductIdAsync(product.Id))
            .ReturnsAsync(new Inventory { ProductId = product.Id, Quantity = 5 });

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(repository => repository.CreateAtomicallyAsync(
                It.IsAny<Order>(),
                It.IsAny<IReadOnlyDictionary<int, int>>(),
                cart.Id,
                cart.Items.Count))
            .Callback<Order, IReadOnlyDictionary<int, int>, int?, int>(
                (order, _, _, _) =>
                {
                    Assert.Equal("Customer", order.CustomerName);
                    Assert.Equal("123 Main Street", order.ShippingAddress);
                    Assert.Equal("1234567890", order.PhoneNumber);
                    Assert.Equal(99.98m, order.TotalAmount);
                    Assert.Equal(49.99m, Assert.Single(order.OrderItems).UnitPrice);
                })
            .ReturnsAsync(savedOrder);
        orderRepository
            .Setup(repository => repository.GetByIdAsync(savedOrder.Id))
            .ReturnsAsync(savedOrder);

        var service = new LegacyOrderService(
            orderRepository.Object,
            productRepository.Object,
            inventoryRepository.Object,
            cartRepository.Object,
            new Mock<IProductService>().Object);

        var result = await service.CreateFromCartAsync(
            12,
            new PlaceOrderFromCartDto
            {
                CustomerName = " Customer ",
                ShippingAddress = " 123 Main Street ",
                PhoneNumber = " 1234567890 "
            });

        Assert.Equal(savedOrder.Id, result.Id);
        Assert.Equal(99.98m, result.TotalAmount);
        orderRepository.Verify(repository => repository.CreateAtomicallyAsync(
            It.IsAny<Order>(),
            It.Is<IReadOnlyDictionary<int, int>>(quantities =>
                quantities.Count == 1 && quantities[product.Id] == 2),
            cart.Id,
            cart.Items.Count), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_UsesAtomicRestockOperation()
    {
        var order = new Order
        {
            Id = 8,
            UserId = 12,
            Status = ECommerce.Domain.Enums.OrderStatus.Pending
        };
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(repository => repository.GetByIdAsync(order.Id))
            .ReturnsAsync(order);
        orderRepository
            .Setup(repository => repository.CancelPendingWithRestockAsync(order.Id, order.UserId))
            .ReturnsAsync(true);

        var service = new LegacyOrderService(
            orderRepository.Object,
            new Mock<IProductRepository>().Object,
            new Mock<IInventoryRepository>().Object,
            new Mock<ICartRepository>().Object,
            new Mock<IProductService>().Object);

        var cancelled = await service.CancelAsync(order.Id, order.UserId);

        Assert.True(cancelled);
        orderRepository.Verify(
            repository => repository.CancelPendingWithRestockAsync(order.Id, order.UserId),
            Times.Once);
        orderRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Order>()),
            Times.Never);
    }
}