using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GreenHerb.Api.Contracts.Auth;
using GreenHerb.Domain.Entities;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GreenHerb.IntegrationTests;

public sealed class CartEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CartEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Returns_Empty_Cart_For_Authorized_User()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("cart_empty");

        var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(payload);
        Assert.Empty(payload.Items);
        Assert.Equal(0, payload.ItemCount);
        Assert.Equal(0m, payload.Subtotal);
    }

    [Fact]
    public async Task AddItem_Adds_And_Increments_Cart_Product()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("cart_add");
        var productId = await SeedProductAsync("Magnesium Glycinate", 24.99m);

        var firstResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId,
            quantity = 1
        });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId,
            quantity = 2
        });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var payload = await secondResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal(3, payload.Items[0].Quantity);
        Assert.Equal(3, payload.ItemCount);
        Assert.Equal(74.97m, payload.Subtotal);
    }

    [Fact]
    public async Task Update_And_Delete_Item_Changes_Cart_State()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("cart_update_delete");
        var productId = await SeedProductAsync("Vitamin C", 19.50m);

        await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId,
            quantity = 1
        });

        var updateResponse = await client.PutAsJsonAsync($"/api/cart/items/{productId}", new
        {
            quantity = 5
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedPayload = await updateResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(updatedPayload);
        Assert.Single(updatedPayload.Items);
        Assert.Equal(5, updatedPayload.Items[0].Quantity);
        Assert.Equal(97.50m, updatedPayload.Subtotal);

        var deleteResponse = await client.DeleteAsync($"/api/cart/items/{productId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deletedPayload = await deleteResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(deletedPayload);
        Assert.Empty(deletedPayload.Items);
        Assert.Equal(0, deletedPayload.ItemCount);
        Assert.Equal(0m, deletedPayload.Subtotal);
    }

    [Fact]
    public async Task Merge_Adds_Guest_Items_Into_Server_Cart()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("cart_merge");
        var firstProductId = await SeedProductAsync("Ashwagandha", 34.99m);
        var secondProductId = await SeedProductAsync("Turmeric", 29.99m);

        await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId = firstProductId,
            quantity = 2
        });

        var mergeResponse = await client.PostAsJsonAsync("/api/cart/merge", new
        {
            items = new[]
            {
                new { productId = firstProductId, quantity = 3 },
                new { productId = secondProductId, quantity = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.OK, mergeResponse.StatusCode);

        var payload = await mergeResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);

        var firstItem = payload.Items.Single(item => item.ProductId == firstProductId);
        var secondItem = payload.Items.Single(item => item.ProductId == secondProductId);

        Assert.Equal(5, firstItem.Quantity);
        Assert.Equal(1, secondItem.Quantity);
        Assert.Equal(6, payload.ItemCount);
        Assert.Equal(204.94m, payload.Subtotal);
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

    private async Task<int> SeedProductAsync(string name, decimal price)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slug = name.ToLowerInvariant().Replace(' ', '-');
        var product = new Product
        {
            Name = name,
            Slug = $"{slug}-{Guid.NewGuid():N}",
            Sku = $"SKU-{Guid.NewGuid():N}"[..20],
            Brand = "GreenHerb",
            Category = "digestive",
            Form = "capsules",
            Dosage = "500mg",
            ServingSize = "1 capsule",
            CountInPack = 60,
            ServingsPerContainer = 60,
            ShortDescription = $"Short description for {name}",
            Description = $"Long description for {name}",
            Ingredients = "Ingredient A, Ingredient B",
            HowToUse = "Take one capsule daily.",
            Warnings = "Keep away from children.",
            ImageUrl = $"/products/{slug}.jpg",
            Price = price,
            CompareAtPrice = null,
            QuantityInStock = 25,
            IsActive = true
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product.Id;
    }

    private sealed class CartResponseDto
    {
        public List<CartItemDto> Items { get; set; } = [];
        public decimal Subtotal { get; set; }
        public int ItemCount { get; set; }
    }

    private sealed class CartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
