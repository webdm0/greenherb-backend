using System.Net;
using System.Net.Http.Json;
using GreenHerb.Domain.Entities;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GreenHerb.IntegrationTests;

public sealed class ProductEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Returns_Paginated_Products_With_Facets()
    {
        await _factory.ResetDatabaseAsync();
        await SeedProductsAsync([
            CreateProduct("Vitamin C", "immunity", "tablets", 18.99m, 4.8m, 124, 450, ["vegan", "non-gmo"], true),
            CreateProduct("Ashwagandha", "stress-sleep", "capsules", 34.99m, 4.7m, 210, 980, ["organic", "vegan"], true),
            CreateProduct("Ginger Root", "digestive", "capsules", 16.99m, 4.4m, 88, 320, ["gluten-free"], false)
        ]);

        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/products?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProductSearchResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Total);
        Assert.Equal(2, payload.PageSize);
        Assert.Equal(2, payload.TotalPages);
        Assert.Equal(2, payload.Items.Count);
        Assert.Contains(payload.Facets.Categories, facet => facet.Value == "immunity" && facet.Count == 1);
        Assert.Contains(payload.Facets.Forms, facet => facet.Value == "capsules" && facet.Count == 2);
        Assert.Equal(16m, payload.Facets.Price.Min);
        Assert.Equal(35m, payload.Facets.Price.Max);
    }

    [Fact]
    public async Task Get_Applies_Filtering_And_Sorting()
    {
        await _factory.ResetDatabaseAsync();
        await SeedProductsAsync([
            CreateProduct("Organic Zinc", "immunity", "capsules", 15.99m, 4.9m, 90, 1500, ["organic", "vegan"], true, compareAtPrice: 19.99m, createdAt: DateTime.UtcNow.AddDays(-5)),
            CreateProduct("Daily Zinc", "immunity", "capsules", 12.99m, 4.2m, 30, 100, ["vegan"], true, createdAt: DateTime.UtcNow.AddDays(-60)),
            CreateProduct("Sleep Tea", "stress-sleep", "teas", 11.99m, 4.8m, 45, 420, ["organic"], false, createdAt: DateTime.UtcNow.AddDays(-3))
        ]);

        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/products?category=immunity&form=capsules&dietary=organic&availability=in-stock&availability=sale&rating=4.5&sort=price-low");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProductSearchResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("Organic Zinc", payload.Items[0].Name);
        Assert.Equal("immunity", payload.Items[0].Category);
        Assert.Contains("Sale", payload.Items[0].Badges);
    }

    [Fact]
    public async Task Get_Scopes_Facets_To_Selected_Category()
    {
        await _factory.ResetDatabaseAsync();
        await SeedProductsAsync([
            CreateProduct("Organic Zinc", "immunity", "capsules", 15.99m, 4.9m, 90, 1500, ["organic", "vegan"], true),
            CreateProduct("Daily Zinc", "immunity", "tablets", 12.99m, 4.2m, 30, 100, ["vegan"], true),
            CreateProduct("Sleep Tea", "stress-sleep", "teas", 11.99m, 4.8m, 45, 420, ["non-gmo"], true)
        ]);

        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/products?category=immunity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProductSearchResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Total);
        Assert.Contains(payload.Facets.Forms, facet => facet.Value == "capsules" && facet.Count == 1);
        Assert.Contains(payload.Facets.Forms, facet => facet.Value == "tablets" && facet.Count == 1);
        Assert.DoesNotContain(payload.Facets.Forms, facet => facet.Value == "teas");
        Assert.Contains(payload.Facets.Dietary, facet => facet.Value == "vegan" && facet.Count == 2);
        Assert.DoesNotContain(payload.Facets.Dietary, facet => facet.Value == "non-gmo");
    }

    [Fact]
    public async Task Get_Applies_Text_Search()
    {
        await _factory.ResetDatabaseAsync();
        await SeedProductsAsync([
            CreateProduct("Calm Ashwagandha", "stress-sleep", "capsules", 34.99m, 4.7m, 210, 980, ["organic", "vegan"], true),
            CreateProduct("Daily Ginger", "digestive", "capsules", 16.99m, 4.4m, 88, 320, ["gluten-free"], true)
        ]);

        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/products?search=ashwagandha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProductSearchResponseDto>();
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("Calm Ashwagandha", payload.Items[0].Name);
    }

    [Fact]
    public async Task GetBySlug_Returns_Product_Details()
    {
        await _factory.ResetDatabaseAsync();
        var product = CreateProduct(
            "Vitamin C 1000mg",
            "immunity",
            "tablets",
            18.99m,
            4.8m,
            124,
            450,
            ["vegan", "non-gmo"],
            true,
            compareAtPrice: 24.99m);

        await SeedProductsAsync([product]);

        var client = _factory.CreateApiClient();
        var response = await client.GetAsync($"/api/products/{product.Slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProductDetailResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(product.Name, payload.Name);
        Assert.Equal(product.Slug, payload.Slug);
        Assert.Equal(product.Sku, payload.Sku);
        Assert.Equal(product.ShortDescription, payload.ShortDescription);
        Assert.Equal(product.QuantityInStock, payload.QuantityInStock);
        Assert.Equal(product.Dietary, payload.Dietary);
    }

    private async Task SeedProductsAsync(IEnumerable<Product> products)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync();
    }

    private static Product CreateProduct(
        string name,
        string category,
        string form,
        decimal price,
        decimal rating,
        int reviewCount,
        int soldCount,
        List<string> dietary,
        bool inStock,
        decimal? compareAtPrice = null,
        DateTime? createdAt = null)
    {
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        return new Product
        {
            Name = name,
            Slug = $"{slug}-{Guid.NewGuid():N}",
            Sku = $"SKU-{Guid.NewGuid():N}"[..20],
            Brand = "GreenHerb",
            Category = category,
            Form = form,
            Dosage = "500mg",
            ServingSize = "1 capsule",
            CountInPack = 60,
            ServingsPerContainer = 60,
            ShortDescription = $"Short description for {name}",
            Description = $"Long description for {name}",
            Ingredients = "Ingredient A, Ingredient B",
            HowToUse = "Take one capsule daily.",
            Warnings = "Keep away from children.",
            ImageUrl = $"https://images.unsplash.com/{slug}",
            Price = price,
            CompareAtPrice = compareAtPrice,
            QuantityInStock = inStock ? 25 : 0,
            Dietary = dietary,
            Rating = rating,
            ReviewCount = reviewCount,
            SoldCount = soldCount,
            IsFeatured = soldCount >= 1000,
            IsActive = true,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-20),
            UpdatedAt = createdAt ?? DateTime.UtcNow.AddDays(-20)
        };
    }

    private sealed class ProductSearchResponseDto
    {
        public List<ProductItemDto> Items { get; set; } = [];
        public int Total { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public ProductFacetsDto Facets { get; set; } = new();
    }

    private sealed class ProductItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Badges { get; set; } = [];
    }

    private sealed class ProductFacetsDto
    {
        public List<FacetOptionDto> Categories { get; set; } = [];
        public List<FacetOptionDto> Forms { get; set; } = [];
        public List<FacetOptionDto> Dietary { get; set; } = [];
        public PriceRangeDto Price { get; set; } = new();
    }

    private sealed class ProductDetailResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public int QuantityInStock { get; set; }
        public List<string> Dietary { get; set; } = [];
    }

    private sealed class FacetOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class PriceRangeDto
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
}
