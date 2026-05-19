using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GreenHerb.Api.Contracts.Auth;
using GreenHerb.Domain.Entities;
using GreenHerb.Domain.Enums;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace GreenHerb.IntegrationTests;

public sealed class OrdersEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Returns_Current_User_Order_History_Only()
    {
        await _factory.ResetDatabaseAsync();
        var firstClient = await CreateAuthorizedClientAsync("orders_first");
        var secondClient = await CreateAuthorizedClientAsync("orders_second");

        var firstUserId = await GetUserIdAsync("orders_first@example.com");
        var secondUserId = await GetUserIdAsync("orders_second@example.com");

        await SeedOrderAsync(firstUserId, "Paid", 89.50m, OrderStatus.Paid, 2);
        await SeedOrderAsync(firstUserId, "Pending", 45.00m, OrderStatus.Pending, 1);
        await SeedOrderAsync(secondUserId, "Pending", 45.00m, OrderStatus.Pending, 1);

        var response = await firstClient.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<OrderHistoryDto>>();
        Assert.NotNull(payload);
        Assert.Single(payload);
        Assert.Equal(8, payload[0].OrderReference.Length);
        Assert.Equal("Paid", payload[0].Status);
        Assert.Equal(89.50m, payload[0].TotalAmount);
        Assert.Equal(2, payload[0].Items.Count);

        _ = secondClient;
    }

    [Fact]
    public async Task SaveChanges_Normalizes_Order_Fields()
    {
        await _factory.ResetDatabaseAsync();
        var userId = await CreateUserAsync("orders_normalize");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            OrderReference = $"NORM{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            UserId = userId,
            Status = OrderStatus.Pending,
            TotalAmount = 42m,
            CustomerName = "   Ivan    Petrov   ",
            CustomerEmail = "   ivan@example.com   ",
            CustomerPhone = "  +1   555   123   4567  ",
            ShippingCountry = "  United   States  ",
            ShippingCity = "  New   York  ",
            ShippingAddressLine1 = "  123   Main   Street  ",
            ShippingAddressLine2 = "  Apt    4B  ",
            ShippingPostalCode = "  SW1A   1AA  ",
            ShippingRegion = "  New   York  ",
            Notes = "  Leave at door.  "
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        Assert.Equal("Ivan Petrov", order.CustomerName);
        Assert.Equal("ivan@example.com", order.CustomerEmail);
        Assert.Equal("+1 555 123 4567", order.CustomerPhone);
        Assert.Equal("United States", order.ShippingCountry);
        Assert.Equal("New York", order.ShippingCity);
        Assert.Equal("123 Main Street", order.ShippingAddressLine1);
        Assert.Equal("Apt 4B", order.ShippingAddressLine2);
        Assert.Equal("SW1A 1AA", order.ShippingPostalCode);
        Assert.Equal("New York", order.ShippingRegion);
        Assert.Equal("Leave at door.", order.Notes);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(string username)
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = $"{username}@example.com",
            Password = "strong-password"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return client;
    }

    private async Task<int> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(existingUser => existingUser.Email == email);
        return user.Id;
    }

    private async Task<int> CreateUserAsync(string username)
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = $"{username}@example.com",
            Password = "strong-password"
        });

        response.EnsureSuccessStatusCode();
        return await GetUserIdAsync($"{username}@example.com");
    }

    private async Task SeedOrderAsync(int userId, string suffix, decimal totalAmount, OrderStatus status, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product
        {
            Name = $"History Product {suffix}",
            Slug = $"history-product-{suffix.ToLowerInvariant()}-{Guid.NewGuid():N}",
            Sku = $"HIST-{Guid.NewGuid():N}"[..20],
            Brand = "GreenHerb",
            Category = "digestive",
            Form = "capsules",
            Dosage = "500mg",
            ServingSize = "1 capsule",
            CountInPack = 30,
            ServingsPerContainer = 30,
            ShortDescription = "Order history item",
            Description = "Order history item description",
            Ingredients = "Ingredient A",
            HowToUse = "Once daily",
            Warnings = "Keep away from children",
            ImageUrl = "/products/ginger.jpg",
            Price = totalAmount / quantity,
            QuantityInStock = 15,
            IsActive = true
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var order = new Order
        {
            OrderReference = $"REF{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            UserId = userId,
            Status = status,
            TotalAmount = totalAmount,
            CustomerName = "Test User",
            CustomerEmail = $"{suffix.ToLowerInvariant()}@example.com",
            CustomerPhone = "+1 555 123 4567",
            ShippingCountry = "United States",
            ShippingCity = "Portland",
            ShippingAddressLine1 = "123 Wellness Ave",
            ShippingPostalCode = "97201",
            ShippingRegion = "Oregon",
            Notes = "Leave at front desk",
            PaidAt = status == OrderStatus.Paid ? DateTime.UtcNow : null,
            Items =
            [
                new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSlug = product.Slug,
                    ProductSku = product.Sku,
                    ProductImageUrl = product.ImageUrl,
                    Quantity = quantity,
                    UnitPrice = product.Price
                },
                new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSlug = product.Slug,
                    ProductSku = product.Sku,
                    ProductImageUrl = product.ImageUrl,
                    Quantity = 1,
                    UnitPrice = 0m
                }
            ]
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
    }

    private sealed class OrderHistoryDto
    {
        public int Id { get; set; }
        public string OrderReference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<OrderHistoryItemDto> Items { get; set; } = [];
    }

    
    private sealed class OrderHistoryItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
    }
}
