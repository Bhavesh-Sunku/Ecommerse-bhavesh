using ECommerce.Business.DTOs.Cart;
using ECommerce.Business.Services;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Moq;

namespace ECommerce.Tests;

public class CartServiceTests
{
    [Fact]
    public async Task AddItemAsync_AddsProductAndReturnsCalculatedSubtotal()
    {
        var product = new Product
        {
            Id = 7,
            Name = "Headphones",
            Price = 49.99m,
            ImageUrl = "headphones.png",
            IsActive = true
        };
        var cart = new Cart { Id = 4, UserId = 12 };
        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync((Cart?)null);
        cartRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Cart>()))
            .ReturnsAsync(cart);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByIdAsync(7))
            .ReturnsAsync(product);

        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repository => repository.GetByProductIdAsync(7))
            .ReturnsAsync(new Inventory { ProductId = 7, Quantity = 5 });

        var service = new CartService(
            cartRepository.Object,
            productRepository.Object,
            inventoryRepository.Object);

        var result = await service.AddItemAsync(
            12,
            new AddCartItemDto { ProductId = 7, Quantity = 2 });

        Assert.Equal(4, result.Id);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].Quantity);
        Assert.Equal(99.98m, result.TotalAmount);
        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_RejectsQuantityAboveAvailableStock()
    {
        var cart = new Cart
        {
            Id = 4,
            UserId = 12,
            Items =
            [
                new CartItem
                {
                    Id = 3,
                    ProductId = 7,
                    Quantity = 2,
                    Product = new Product { Id = 7, Name = "Headphones", IsActive = true }
                }
            ]
        };

        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByIdAsync(7))
            .ReturnsAsync(new Product { Id = 7, IsActive = true });

        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repository => repository.GetByProductIdAsync(7))
            .ReturnsAsync(new Inventory { ProductId = 7, Quantity = 3 });

        var service = new CartService(
            cartRepository.Object,
            productRepository.Object,
            inventoryRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddItemAsync(12, new AddCartItemDto { ProductId = 7, Quantity = 2 }));

        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateItemAsync_ChangesQuantityAndRecalculatesTotal()
    {
        var product = new Product
        {
            Id = 7,
            Name = "Headphones",
            Price = 49.99m,
            IsActive = true,
            Inventory = new Inventory { ProductId = 7, Quantity = 5 }
        };
        var item = new CartItem { Id = 3, ProductId = 7, Product = product, Quantity = 1 };
        var cart = new Cart { Id = 4, UserId = 12, Items = [item] };
        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repository => repository.GetByProductIdAsync(7))
            .ReturnsAsync(product.Inventory);

        var service = new CartService(
            cartRepository.Object,
            new Mock<IProductRepository>().Object,
            inventoryRepository.Object);

        var result = await service.UpdateItemAsync(12, item.Id, 3);

        Assert.Equal(3, item.Quantity);
        Assert.Equal(149.97m, result.TotalAmount);
        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateItemAsync_RejectsQuantityAboveAvailableStock()
    {
        var product = new Product
        {
            Id = 7,
            Name = "Headphones",
            IsActive = true,
            Inventory = new Inventory { ProductId = 7, Quantity = 2 }
        };
        var item = new CartItem { Id = 3, ProductId = 7, Product = product, Quantity = 1 };
        var cart = new Cart { Id = 4, UserId = 12, Items = [item] };
        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repository => repository.GetByProductIdAsync(7))
            .ReturnsAsync(product.Inventory);

        var service = new CartService(
            cartRepository.Object,
            new Mock<IProductRepository>().Object,
            inventoryRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateItemAsync(12, item.Id, 3));

        Assert.Equal(1, item.Quantity);
        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesItemFromCurrentCart()
    {
        var item = new CartItem
        {
            Id = 3,
            ProductId = 7,
            Product = new Product { Id = 7, Name = "Headphones" },
            Quantity = 1
        };
        var cart = new Cart { Id = 4, UserId = 12, Items = [item] };
        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var service = new CartService(
            cartRepository.Object,
            new Mock<IProductRepository>().Object,
            new Mock<IInventoryRepository>().Object);

        var result = await service.RemoveItemAsync(12, item.Id);

        Assert.Empty(cart.Items);
        Assert.Empty(result.Items);
        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllCurrentCartItems()
    {
        var cart = new Cart
        {
            Id = 4,
            UserId = 12,
            Items =
            [
                new CartItem
                {
                    Id = 3,
                    ProductId = 7,
                    Product = new Product { Id = 7, Name = "Headphones" },
                    Quantity = 1
                }
            ]
        };
        var cartRepository = new Mock<ICartRepository>();
        cartRepository
            .Setup(repository => repository.GetByUserIdAsync(12))
            .ReturnsAsync(cart);

        var service = new CartService(
            cartRepository.Object,
            new Mock<IProductRepository>().Object,
            new Mock<IInventoryRepository>().Object);

        var result = await service.ClearAsync(12);

        Assert.Empty(cart.Items);
        Assert.Empty(result.Items);
        cartRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }
}
