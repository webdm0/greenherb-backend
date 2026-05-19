using GreenHerb.Application.Features.Products.Dtos;

namespace GreenHerb.Application.Features.Products.Interfaces;

public interface IProductCatalogService
{
    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<List<string>> GetAllActiveSlugsAsync(CancellationToken cancellationToken = default);
}
