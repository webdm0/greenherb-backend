using System.Text.Json.Serialization;

namespace GreenHerb.Application.Features.Products.Dtos;

public sealed class ProductSearchQuery
{
    public List<string> Category { get; set; } = [];
    public string? Search { get; set; }
    public List<string> Form { get; set; } = [];
    public List<string> Dietary { get; set; } = [];
    public List<string> Availability { get; set; } = [];
    public decimal Min { get; set; } = 4m;
    public decimal Max { get; set; } = 48m;
    public decimal? Rating { get; set; }
    public string? Sort { get; set; } = "featured";
    public int Page { get; set; } = 1;
    public int? PageSize { get; set; }
}

public sealed class ProductSearchResult
{
    public List<ProductCardDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ProductFacetsDto Facets { get; set; } = new();
}

public sealed class ProductCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Image { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public List<string> Badges { get; set; } = [];
    public bool InStock { get; set; }
    public bool Organic { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public List<string> Dietary { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public int SoldCount { get; set; }

    [JsonIgnore]
    public string SearchText { get; set; } = string.Empty;
}

public sealed class ProductDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string ServingSize { get; set; } = string.Empty;
    public int? CountInPack { get; set; }
    public int? ServingsPerContainer { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public string HowToUse { get; set; } = string.Empty;
    public string Warnings { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int QuantityInStock { get; set; }
    public List<string> Dietary { get; set; } = [];
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public int SoldCount { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProductFacetsDto
{
    public List<FacetOptionDto> Categories { get; set; } = [];
    public List<FacetOptionDto> Forms { get; set; } = [];
    public List<FacetOptionDto> Dietary { get; set; } = [];
    public List<FacetOptionDto> Availability { get; set; } = [];
    public List<FacetOptionDto> Ratings { get; set; } = [];
    public PriceRangeDto Price { get; set; } = new();
}

public sealed class FacetOptionDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class PriceRangeDto
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
}
