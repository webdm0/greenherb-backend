using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Application.Features.Products.Dtos;
using GreenHerb.Application.Features.Products.Interfaces;
using GreenHerb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GreenHerb.Application.Features.Products.Services;

public sealed class ProductCatalogService(IAppDbContext dbContext) : IProductCatalogService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 9;
    private const int MaxPageSize = 36;
    private static readonly string[] RatingFacetValues = ["4", "3", "2"];

    private static readonly Dictionary<string, string> CategoryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["immunity"] = "Immunity Support",
        ["digestive"] = "Digestive Health",
        ["stress-sleep"] = "Stress & Sleep",
        ["energy"] = "Energy & Vitality",
        ["joint-mobility"] = "Joint & Mobility"
    };

    private static readonly Dictionary<string, string> FormLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["capsules"] = "Capsules",
        ["tablets"] = "Tablets",
        ["powders"] = "Powders",
        ["liquid-extracts"] = "Liquid Extracts",
        ["gummies"] = "Gummies",
        ["teas"] = "Teas"
    };

    private static readonly Dictionary<string, string> DietaryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["organic"] = "Organic Certified",
        ["vegan"] = "Vegan",
        ["gluten-free"] = "Gluten-Free",
        ["non-gmo"] = "Non-GMO",
        ["sugar-free"] = "Sugar-Free"
    };

    private static readonly Dictionary<string, string> AvailabilityLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["in-stock"] = "In Stock",
        ["sale"] = "On Sale",
        ["new"] = "New Arrivals"
    };

    public async Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsActive && item.Slug == slug, cancellationToken);

        return product is null ? null : MapProductDetail(product);
    }

    public async Task<List<string>> GetAllActiveSlugsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .Select(product => product.Slug)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var activeProducts = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .ToListAsync(cancellationToken);

        var catalog = activeProducts
            .Select(MapProduct)
            .ToList();

        var filtered = ApplyFilters(catalog, query);
        var sorted = ApplySorting(filtered, query.Sort);
        var pageSize = NormalizePageSize(query.PageSize);
        var total = sorted.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(Math.Max(query.Page, DefaultPage), totalPages);
        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ProductSearchResult
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Facets = BuildFacets(catalog, query)
        };
    }

    private static List<ProductCardDto> ApplyFilters(List<ProductCardDto> products, ProductSearchQuery query)
    {
        return products.Where(product =>
        {
            if (!MatchesSearch(product, query.Search))
            {
                return false;
            }

            if (query.Category.Count > 0 && !query.Category.Contains(product.Category, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.Form.Count > 0 && !query.Form.Contains(product.Form, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.Dietary.Count > 0 && query.Dietary.Any(value => !product.Dietary.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (query.Availability.Count > 0 && query.Availability.Any(value => !MatchesAvailability(product, value)))
            {
                return false;
            }

            if (query.Rating.HasValue && product.Rating < query.Rating.Value)
            {
                return false;
            }

            return product.Price >= query.Min && product.Price <= query.Max;
        }).ToList();
    }

    private static bool MatchesSearch(ProductCardDto product, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return product.SearchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<ProductCardDto> ApplySorting(List<ProductCardDto> items, string? sort)
    {
        return (sort ?? "featured").ToLowerInvariant() switch
        {
            "newest" => items.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.SoldCount).ToList(),
            "price-low" => items.OrderBy(product => product.Price).ThenByDescending(product => product.Rating).ToList(),
            "price-high" => items.OrderByDescending(product => product.Price).ThenByDescending(product => product.Rating).ToList(),
            "rating" => items.OrderByDescending(product => product.Rating).ThenByDescending(product => product.ReviewCount).ToList(),
            "bestselling" => items.OrderByDescending(product => product.SoldCount).ThenByDescending(product => product.Rating).ToList(),
            _ => items
                .OrderByDescending(product => product.Badges.Contains("Popular"))
                .ThenByDescending(product => product.SoldCount)
                .ThenByDescending(product => product.Rating)
                .ToList()
        };
    }

    private static ProductDetailDto MapProductDetail(Product product)
    {
        return new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Sku = product.Sku,
            Brand = product.Brand,
            Category = product.Category,
            Form = product.Form,
            Dosage = product.Dosage,
            ServingSize = product.ServingSize,
            CountInPack = product.CountInPack,
            ServingsPerContainer = product.ServingsPerContainer,
            ShortDescription = product.ShortDescription,
            Description = product.Description,
            Ingredients = product.Ingredients,
            HowToUse = product.HowToUse,
            Warnings = product.Warnings,
            ImageUrl = product.ImageUrl,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            QuantityInStock = product.QuantityInStock,
            Dietary = product.Dietary,
            Rating = product.Rating,
            ReviewCount = product.ReviewCount,
            SoldCount = product.SoldCount,
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private static ProductCardDto MapProduct(Product product)
    {
        var badges = new List<string>();
        var isNew = product.ReviewCount < 40;
        var isOrganic = product.Dietary.Contains("organic", StringComparer.OrdinalIgnoreCase);

        if (product.SoldCount >= 1000)
        {
            badges.Add("Best Seller");
        }

        if (isNew)
        {
            badges.Add("New");
        }

        if (product.IsFeatured)
        {
            badges.Add("Popular");
        }

        if (product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.Price)
        {
            badges.Add("Sale");
        }

        return new ProductCardDto
        {
            Id = product.Id.ToString(),
            Slug = product.Slug,
            Name = product.Name,
            Description = product.ShortDescription,
            Price = product.Price,
            OriginalPrice = product.CompareAtPrice,
            Image = product.ImageUrl ?? string.Empty,
            Rating = product.Rating,
            ReviewCount = product.ReviewCount,
            Badges = badges,
            InStock = product.QuantityInStock > 0,
            Organic = isOrganic,
            Category = product.Category,
            Form = product.Form,
            Dietary = product.Dietary,
            CreatedAt = product.CreatedAt,
            SoldCount = product.SoldCount,
            SearchText = string.Join(
                ' ',
                new[]
                {
                    product.Name,
                    product.Brand,
                    product.Category,
                    product.Form,
                    product.ShortDescription,
                    product.Description,
                    product.Ingredients
                }.Where(value => !string.IsNullOrWhiteSpace(value)))
        };
    }

    private static ProductFacetsDto BuildFacets(List<ProductCardDto> products, ProductSearchQuery query)
    {
        var categoryQuery = CloneQuery(query);
        categoryQuery.Category = [];

        var formQuery = CloneQuery(query);
        formQuery.Form = [];

        var dietaryQuery = CloneQuery(query);
        dietaryQuery.Dietary = [];

        var availabilityQuery = CloneQuery(query);
        availabilityQuery.Availability = [];

        var ratingQuery = CloneQuery(query);
        ratingQuery.Rating = null;

        var priceQuery = CloneQuery(query);
        priceQuery.Min = 4m;
        priceQuery.Max = 48m;

        var categoryProducts = ApplyFilters(products, categoryQuery);
        var formProducts = ApplyFilters(products, formQuery);
        var dietaryProducts = ApplyFilters(products, dietaryQuery);
        var availabilityProducts = ApplyFilters(products, availabilityQuery);
        var ratingProducts = ApplyFilters(products, ratingQuery);
        var priceProducts = ApplyFilters(products, priceQuery);
        var priceMin = priceProducts.Count > 0 ? Math.Floor(priceProducts.Min(product => product.Price)) : 4m;
        var priceMax = priceProducts.Count > 0 ? Math.Ceiling(priceProducts.Max(product => product.Price)) : 48m;

        return new ProductFacetsDto
        {
            Categories = MakeFacet(CategoryLabels, key => categoryProducts.Count(product => product.Category.Equals(key, StringComparison.OrdinalIgnoreCase))),
            Forms = MakeFacet(FormLabels, key => formProducts.Count(product => product.Form.Equals(key, StringComparison.OrdinalIgnoreCase))),
            Dietary = MakeFacet(DietaryLabels, key => dietaryProducts.Count(product => product.Dietary.Contains(key, StringComparer.OrdinalIgnoreCase))),
            Availability = MakeFacet(AvailabilityLabels, key => availabilityProducts.Count(product => MatchesAvailability(product, key))),
            Ratings = RatingFacetValues
                .Select(value => new FacetOptionDto
                {
                    Label = $"{value} Stars & Up",
                    Value = value,
                    Count = ratingProducts.Count(product => product.Rating >= decimal.Parse(value))
                })
                .Where(option => option.Count > 0)
                .ToList(),
            Price = new PriceRangeDto
            {
                Min = priceMin,
                Max = priceMax
            }
        };
    }

    private static ProductSearchQuery CloneQuery(ProductSearchQuery query)
    {
        return new ProductSearchQuery
        {
            Category = [.. query.Category],
            Search = query.Search,
            Form = [.. query.Form],
            Dietary = [.. query.Dietary],
            Availability = [.. query.Availability],
            Min = query.Min,
            Max = query.Max,
            Rating = query.Rating,
            Sort = query.Sort,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static List<FacetOptionDto> MakeFacet(Dictionary<string, string> labels, Func<string, int> count)
    {
        return labels
            .Select(pair => new FacetOptionDto
            {
                Label = pair.Value,
                Value = pair.Key,
                Count = count(pair.Key)
            })
            .Where(option => option.Count > 0)
            .ToList();
    }

    private static bool MatchesAvailability(ProductCardDto product, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "in-stock" => product.InStock,
            "sale" => product.OriginalPrice.HasValue,
            "new" => product.Badges.Contains("New"),
            _ => false
        };
    }

    private static int NormalizePageSize(int? pageSize)
    {
        if (!pageSize.HasValue || pageSize.Value <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(pageSize.Value, MaxPageSize);
    }
}
