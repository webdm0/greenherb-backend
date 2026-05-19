using System.Text.Json;
using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenHerb.Infrastructure.Persistence.Seed;

public sealed class ProductSeeder(
    IAppDbContext dbContext,
    ILogger<ProductSeeder> logger)
{
    private const string RelativeCatalogPath = "Seed/products.catalog.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Skipping product seed because the catalog already contains products.");
            return;
        }

        var catalogPath = Path.Combine(AppContext.BaseDirectory, RelativeCatalogPath);
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException($"Product catalog seed file was not found at '{catalogPath}'.", catalogPath);
        }

        await using var stream = File.OpenRead(catalogPath);
        var products = await JsonSerializer.DeserializeAsync<List<Product>>(stream, JsonOptions, cancellationToken)
            ?? [];

        if (products.Count == 0)
        {
            logger.LogWarning("Product catalog seed file '{CatalogPath}' is empty. No products were imported.", catalogPath);
            return;
        }

        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {ProductsCount} products from '{CatalogPath}'.", products.Count, catalogPath);
    }
}
