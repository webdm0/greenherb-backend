using System.ComponentModel.DataAnnotations;
using GreenHerb.Application.Features.Products.Dtos;
using GreenHerb.Application.Features.Products.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GreenHerb.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController(IProductCatalogService productCatalogService) : ControllerBase
{
    private const int DefaultPage = 1;

    [HttpGet("all-slugs")]
    public async Task<ActionResult<List<string>>> GetAllSlugs(CancellationToken cancellationToken)
    {
        var slugs = await productCatalogService.GetAllActiveSlugsAsync(cancellationToken);
        return Ok(slugs);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var product = await productCatalogService.GetBySlugAsync(slug, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ProductSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await productCatalogService.SearchAsync(
            new ProductSearchQuery
            {
                Category = request.Category,
                Search = request.Search,
                Form = request.Form,
                Dietary = request.Dietary,
                Availability = request.Availability,
                Min = request.Min,
                Max = request.Max,
                Rating = request.Rating,
                Sort = request.Sort,
                Page = request.Page,
                PageSize = request.PageSize
            },
            cancellationToken);

        return Ok(result);
    }

    public sealed class ProductSearchRequest
    {
        [FromQuery(Name = "category")]
        public List<string> Category { get; set; } = [];

        [FromQuery(Name = "search")]
        public string? Search { get; set; }

        [FromQuery(Name = "form")]
        public List<string> Form { get; set; } = [];

        [FromQuery(Name = "dietary")]
        public List<string> Dietary { get; set; } = [];

        [FromQuery(Name = "availability")]
        public List<string> Availability { get; set; } = [];

        [FromQuery(Name = "min")]
        public decimal Min { get; set; } = 4m;

        [FromQuery(Name = "max")]
        public decimal Max { get; set; } = 48m;

        [FromQuery(Name = "rating")]
        public decimal? Rating { get; set; }

        [FromQuery(Name = "sort")]
        public string? Sort { get; set; } = "featured";

        [FromQuery(Name = "page")]
        public int Page { get; set; } = DefaultPage;

        [FromQuery(Name = "pageSize")]
        public int? PageSize { get; set; }
    }
}
