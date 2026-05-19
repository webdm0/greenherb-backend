using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GreenHerb.Api.Contracts.Auth;
using GreenHerb.Application.Abstractions.Auth;
using GreenHerb.Domain.Entities;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GreenHerb.IntegrationTests;

public sealed class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Creates_User_Cart_Session_And_Returns_Tokens()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "alice_store",
            Email = "alice@example.com",
            Password = "strong-password"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), header => header.Contains("refreshToken="));

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.AccessToken);
        Assert.Equal("alice_store", payload.User.Username);
        Assert.Equal("alice@example.com", payload.User.Email);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Carts.CountAsync());
        Assert.Equal(1, await dbContext.RefreshSessions.CountAsync());
    }

    [Fact]
    public async Task Login_With_Username_Returns_Tokens()
    {
        await _factory.ResetDatabaseAsync();
        await SeedUserAsync("bob_store", "bob@example.com", "strong-password");
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = "bob_store",
            Password = "strong-password"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("bob_store", payload.User.Username);
        Assert.NotEmpty(payload.AccessToken);
    }

    [Fact]
    public async Task Login_Merges_Guest_Cart_Items_Into_User_Cart()
    {
        await _factory.ResetDatabaseAsync();
        var productId = await SeedProductAsync("Omega 3", 27.50m);
        await SeedUserAsync("merge_user", "merge@example.com", "strong-password");
        await SeedCartItemAsync("merge@example.com", productId, 2, 27.50m);
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = "merge_user",
            Password = "strong-password",
            CartItems =
            [
                new() { ProductId = productId, Quantity = 3 }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cartItem = await dbContext.CartItems.SingleAsync(item => item.ProductId == productId);
        Assert.Equal(5, cartItem.Quantity);
    }

    [Fact]
    public async Task Register_Merges_Guest_Cart_Items_Into_New_User_Cart()
    {
        await _factory.ResetDatabaseAsync();
        var firstProductId = await SeedProductAsync("Zinc", 14.25m);
        var secondProductId = await SeedInactiveProductAsync("Hidden Product", 12.00m);
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "guest_merge_user",
            Email = "guest_merge@example.com",
            Password = "strong-password",
            CartItems =
            [
                new() { ProductId = firstProductId, Quantity = 2 },
                new() { ProductId = firstProductId, Quantity = 1 },
                new() { ProductId = secondProductId, Quantity = 5 }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cart = await dbContext.Carts
            .Include(existingCart => existingCart.Items)
            .SingleAsync();

        Assert.Single(cart.Items);
        Assert.Equal(firstProductId, cart.Items[0].ProductId);
        Assert.Equal(3, cart.Items[0].Quantity);
    }

    [Fact]
    public async Task Session_With_RefreshCookie_Returns_Current_User()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateApiClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "charlie_store",
            Email = "charlie@example.com",
            Password = "strong-password"
        });

        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal("charlie_store", payload.Username);
    }

    [Fact]
    public async Task Refresh_Rotates_Refresh_Session_And_Returns_New_AccessToken()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateApiClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "diana_store",
            Email = "diana@example.com",
            Password = "strong-password"
        });

        string originalTokenHash;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            originalTokenHash = await dbContext.RefreshSessions
                .Select(s => s.TokenHash)
                .SingleAsync();
        }

        var response = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.AccessToken);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshSessions = await verifyDbContext.RefreshSessions.ToListAsync();
        Assert.Single(refreshSessions);
        Assert.NotEqual(originalTokenHash, refreshSessions[0].TokenHash);
    }

    [Fact]
    public async Task Me_With_BearerToken_Returns_Current_User()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateApiClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "eva_store",
            Email = "eva@example.com",
            Password = "strong-password"
        });

        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal("eva_store", payload.Username);
    }

    [Fact]
    public async Task Logout_Removes_Session_And_Invalidates_Cookie()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateApiClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "frank_store",
            Email = "frank@example.com",
            Password = "strong-password"
        });

        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var sessionResponse = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await dbContext.RefreshSessions.CountAsync());
    }

    private async Task SeedUserAsync(string username, string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        dbContext.Users.Add(new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Cart = new Cart()
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task<int> SeedProductAsync(string name, decimal price, bool isActive = true)
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
            IsActive = isActive
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product.Id;
    }

    private Task<int> SeedInactiveProductAsync(string name, decimal price)
    {
        return SeedProductAsync(name, price, isActive: false);
    }

    private async Task SeedCartItemAsync(string email, int productId, int quantity, decimal unitPrice)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cart = await dbContext.Carts
            .Include(existingCart => existingCart.User)
            .Include(existingCart => existingCart.Items)
            .SingleAsync(existingCart => existingCart.User.Email == email);

        cart.Items.Add(new CartItem
        {
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        });

        await dbContext.SaveChangesAsync();
    }
}
