using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using GreenHerb.Api.Contracts.Auth;
using GreenHerb.Domain.Enums;
using GreenHerb.Domain.Entities;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StripeEventUtility = Stripe.EventUtility;

namespace GreenHerb.IntegrationTests;

public sealed class CheckoutEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CheckoutEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePaymentIntent_Returns_BadRequest_For_Invalid_Checkout_Fields()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("checkout_validation");

        var response = await client.PostAsJsonAsync("/api/checkout/payment-intent", new
        {
            customerName = "Test User",
            customerEmail = "buyer@example.com",
            customerPhone = "abc123",
            shippingCountry = "United States",
            shippingCity = "Portland",
            shippingAddressLine1 = "123 Wellness Ave",
            shippingPostalCode = "SW1A 1AA"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(payload);
        Assert.Contains("CustomerPhone", payload.Errors.Keys);
        Assert.DoesNotContain("ShippingPostalCode", payload.Errors.Keys);
    }

    [Fact]
    public async Task Quote_Returns_Recalculated_Totals_For_Valid_Cart()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("checkout_quote");
        var productId = await SeedProductAsync("Magnesium Glycinate", 24.99m);

        var addToCartResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId,
            quantity = 3
        });

        addToCartResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/checkout/quote", new
        {
            discountCode = string.Empty
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CheckoutQuoteResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("usd", payload.Currency);
        Assert.Equal(74.97m, payload.Subtotal);
        Assert.Equal(0m, payload.DiscountAmount);
        Assert.Null(payload.DiscountCode);
        Assert.Equal(12m, payload.ShippingAmount);
        Assert.Equal(86.97m, payload.TotalAmount);
    }

    [Fact]
    public async Task StripeWebhook_Marks_Order_As_Paid_And_Clears_Cart()
    {
        await _factory.ResetDatabaseAsync();
        var client = await CreateAuthorizedClientAsync("checkout_webhook");
        var productId = await SeedProductAsync("Omega 3", 19.99m);
        var userId = await GetUserIdAsync("checkout_webhook@example.com");
        var paymentIntentId = "pi_test_webhook_123";

        await client.PostAsJsonAsync("/api/cart/items", new
        {
            productId,
            quantity = 2
        });

        var orderId = await SeedPendingOrderAsync(userId, productId, paymentIntentId, 39.98m);

        var payload = BuildPaymentIntentSucceededPayload(orderId, userId, paymentIntentId, 3998);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = StripeEventUtility.ComputeSignature("whsec_test_placeholder", timestamp, payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook");
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await dbContext.Orders.FindAsync(orderId);
        var cart = await dbContext.Carts
            .Include(existingCart => existingCart.Items)
            .SingleAsync(existingCart => existingCart.UserId == userId);

        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Paid, order!.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Empty(cart.Items);
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

    private async Task<int> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(existingUser => existingUser.Email == email);
        return user.Id;
    }

    private async Task<int> SeedPendingOrderAsync(int userId, int productId, string paymentIntentId, decimal totalAmount)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await dbContext.Products.SingleAsync(existingProduct => existingProduct.Id == productId);

        var order = new Order
        {
            OrderReference = "TEST1234",
            UserId = userId,
            Status = OrderStatus.Pending,
            Currency = "usd",
            SubtotalAmount = totalAmount,
            DiscountAmount = 0m,
            TotalAmount = totalAmount,
            ShippingAmount = 0m,
            PaymentIntentId = paymentIntentId,
            CustomerName = "Test User",
            CustomerEmail = "checkout_webhook@example.com",
            CustomerPhone = "+1234567890",
            ShippingCountry = "United States",
            ShippingCity = "Portland",
            ShippingAddressLine1 = "123 Wellness Ave",
            ShippingPostalCode = "97201",
            Items =
            [
                new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSlug = product.Slug,
                    ProductSku = product.Sku,
                    ProductImageUrl = product.ImageUrl,
                    Quantity = 2,
                    UnitPrice = product.Price
                }
            ]
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private static string BuildPaymentIntentSucceededPayload(int orderId, int userId, string paymentIntentId, long amount)
    {
        var payload = new
        {
            id = "evt_test_webhook_123",
            @object = "event",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = paymentIntentId,
                    @object = "payment_intent",
                    status = "succeeded",
                    amount,
                    currency = "usd",
                    metadata = new Dictionary<string, string>
                    {
                        ["orderId"] = orderId.ToString(),
                        ["userId"] = userId.ToString(),
                        ["totalAmount"] = "39.98",
                        ["currency"] = "usd"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class CheckoutQuoteResponseDto
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountCode { get; set; }
        public decimal ShippingAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
