using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Cart.Dtos;
using GreenHerb.Application.Features.Cart.Interfaces;
using Microsoft.EntityFrameworkCore;
using DomainCart = GreenHerb.Domain.Entities.Cart;
using DomainCartItem = GreenHerb.Domain.Entities.CartItem;

namespace GreenHerb.Application.Features.Cart.Services;

public sealed class CartService(IAppDbContext dbContext) : ICartService
{
    public async Task<CartDto> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        return MapCartResponse(cart);
    }

    public async Task<CartDto> AddItemAsync(int userId, AddCartItemCommand command, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var product = await dbContext.Products
            .SingleOrDefaultAsync(p => p.Id == command.ProductId && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException();
        }

        var existingItem = cart.Items.SingleOrDefault(item => item.ProductId == command.ProductId);
        if (existingItem is null)
        {
            cart.Items.Add(new DomainCartItem
            {
                ProductId = product.Id,
                Quantity = command.Quantity,
                UnitPrice = product.Price
            });
        }
        else
        {
            existingItem.Quantity += command.Quantity;
            existingItem.UnitPrice = product.Price;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var updatedCart = await LoadCartAsync(cart.UserId, cancellationToken);
        return MapCartResponse(updatedCart);
    }

    public async Task<CartDto> UpdateItemAsync(int userId, int productId, UpdateCartItemCommand command, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var existingItem = cart.Items.SingleOrDefault(item => item.ProductId == productId);

        if (existingItem is null)
        {
            throw new CartItemNotFoundException();
        }

        existingItem.Quantity = command.Quantity;
        existingItem.UnitPrice = existingItem.Product.Price;

        await dbContext.SaveChangesAsync(cancellationToken);
        var updatedCart = await LoadCartAsync(cart.UserId, cancellationToken);
        return MapCartResponse(updatedCart);
    }

    public async Task<CartDto> RemoveItemAsync(int userId, int productId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var existingItem = cart.Items.SingleOrDefault(item => item.ProductId == productId);

        if (existingItem is not null)
        {
            cart.Items.Remove(existingItem);
            dbContext.CartItems.Remove(existingItem);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var updatedCart = await LoadCartAsync(cart.UserId, cancellationToken);
        return MapCartResponse(updatedCart);
    }

    public async Task<CartDto> ClearAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        if (cart.Items.Count > 0)
        {
            dbContext.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var updatedCart = await LoadCartAsync(cart.UserId, cancellationToken);
        return MapCartResponse(updatedCart);
    }

    public async Task<CartDto> MergeAsync(int userId, MergeCartCommand command, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var itemsToMerge = command.Items
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();

        if (itemsToMerge.Count == 0)
        {
            return MapCartResponse(cart);
        }

        var productIds = itemsToMerge.Select(item => item.ProductId).ToArray();
        var products = await dbContext.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var item in itemsToMerge)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var existingItem = cart.Items.SingleOrDefault(cartItem => cartItem.ProductId == item.ProductId);
            if (existingItem is null)
            {
                cart.Items.Add(new DomainCartItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });

                continue;
            }

            existingItem.Quantity += item.Quantity;
            existingItem.UnitPrice = product.Price;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var updatedCart = await LoadCartAsync(cart.UserId, cancellationToken);
        return MapCartResponse(updatedCart);
    }

    private async Task<DomainCart> GetOrCreateCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await LoadOptionalCartAsync(userId, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = new DomainCart
        {
            UserId = userId
        };

        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadCartAsync(userId, cancellationToken);
    }

    private async Task<DomainCart?> LoadOptionalCartAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Carts
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .SingleOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);
    }

    private async Task<DomainCart> LoadCartAsync(int userId, CancellationToken cancellationToken)
    {
        return await LoadOptionalCartAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"Cart for user {userId} was not found.");
    }

    private static CartDto MapCartResponse(DomainCart cart)
    {
        var orderedItems = cart.Items
            .OrderBy(item => item.ProductId)
            .Select(item => new CartItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Product = new CartProductDto
                {
                    Id = item.Product.Id,
                    Name = item.Product.Name,
                    Description = item.Product.ShortDescription,
                    ImageUrl = item.Product.ImageUrl,
                    Price = item.Product.Price,
                    CompareAtPrice = item.Product.CompareAtPrice,
                    Category = item.Product.Category,
                    Form = item.Product.Form,
                    InStock = item.Product.QuantityInStock > 0,
                    CreatedAt = item.Product.CreatedAt
                }
            })
            .ToList();

        return new CartDto
        {
            Items = orderedItems,
            Subtotal = orderedItems.Sum(item => item.UnitPrice * item.Quantity),
            ItemCount = orderedItems.Sum(item => item.Quantity),
            UpdatedAt = cart.UpdatedAt
        };
    }
}
