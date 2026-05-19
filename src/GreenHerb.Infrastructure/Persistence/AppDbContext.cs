using System.Text.Json;
using System.Text.RegularExpressions;
using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GreenHerb.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public override int SaveChanges()
    {
        PrepareEntities();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var dietaryConverter = new ValueConverter<List<string>, string>(
            dietary => JsonSerializer.Serialize(dietary, JsonOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>());

        var dietaryComparer = new ValueComparer<List<string>>(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            dietary => (dietary ?? new List<string>()).Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
            dietary => (dietary ?? new List<string>()).ToList());

        modelBuilder.Entity<User>()
            .HasIndex(u => u.NormalizedUsername)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        modelBuilder.Entity<ExternalIdentity>()
            .HasIndex(i => new { i.Provider, i.ProviderUserId })
            .IsUnique();

        modelBuilder.Entity<ExternalIdentity>()
            .HasOne(i => i.User)
            .WithMany(u => u.ExternalIdentities)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshSession>()
            .HasIndex(s => s.TokenHash)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Slug)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.CompareAtPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.Rating)
            .HasPrecision(3, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.Dietary)
            .HasConversion(dietaryConverter)
            .Metadata.SetValueComparer(dietaryComparer);

        modelBuilder.Entity<Cart>()
            .HasOne(c => c.User)
            .WithOne(u => u.Cart)
            .HasForeignKey<Cart>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CartItem>()
            .HasIndex(ci => new { ci.CartId, ci.ProductId })
            .IsUnique();

        modelBuilder.Entity<CartItem>()
            .Property(ci => ci.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.Product)
            .WithMany(p => p.CartItems)
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderReference)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderReference)
            .HasMaxLength(8);

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        modelBuilder.Entity<Order>()
            .Property(o => o.Currency)
            .HasMaxLength(3);

        modelBuilder.Entity<Order>()
            .Property(o => o.SubtotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.DiscountAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.DiscountCode)
            .HasMaxLength(100);

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.ShippingAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.PaymentIntentId)
            .HasMaxLength(255);

        modelBuilder.Entity<OrderItem>()
            .Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void PrepareEntities()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.Username = NormalizeForStorage(entry.Entity.Username);
            entry.Entity.Email = NormalizeForStorage(entry.Entity.Email);
            entry.Entity.NormalizedUsername = NormalizeForLookup(entry.Entity.Username);
            entry.Entity.NormalizedEmail = NormalizeForLookup(entry.Entity.Email);
            entry.Entity.UpdatedAt = now;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ExternalIdentity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.Provider = NormalizeForStorage(entry.Entity.Provider);
            entry.Entity.ProviderUserId = NormalizeForStorage(entry.Entity.ProviderUserId);
            entry.Entity.Email = NormalizeForStorage(entry.Entity.Email);
            entry.Entity.DisplayName = NormalizeNullableForStorage(entry.Entity.DisplayName);
            entry.Entity.AvatarUrl = NormalizeNullableForStorage(entry.Entity.AvatarUrl);
            entry.Entity.UpdatedAt = now;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.Name = NormalizeForStorage(entry.Entity.Name);
            entry.Entity.Slug = NormalizeForStorage(entry.Entity.Slug);
            entry.Entity.Brand = NormalizeForStorage(entry.Entity.Brand);
            entry.Entity.Category = NormalizeForStorage(entry.Entity.Category);
            entry.Entity.Form = NormalizeForStorage(entry.Entity.Form);
            entry.Entity.Dosage = NormalizeForStorage(entry.Entity.Dosage);
            entry.Entity.ServingSize = NormalizeForStorage(entry.Entity.ServingSize);
            entry.Entity.Sku = NormalizeForStorage(entry.Entity.Sku);
            entry.Entity.ShortDescription = NormalizeForStorage(entry.Entity.ShortDescription);
            entry.Entity.Description = NormalizeForStorage(entry.Entity.Description);
            entry.Entity.Ingredients = NormalizeForStorage(entry.Entity.Ingredients);
            entry.Entity.HowToUse = NormalizeForStorage(entry.Entity.HowToUse);
            entry.Entity.Warnings = NormalizeForStorage(entry.Entity.Warnings);
            entry.Entity.ImageUrl = NormalizeNullableForStorage(entry.Entity.ImageUrl);
            entry.Entity.Dietary = NormalizeStringList(entry.Entity.Dietary);
            entry.Entity.UpdatedAt = now;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Cart>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.UpdatedAt = now;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Order>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            entry.Entity.CustomerName = NormalizeSingleLineForStorage(entry.Entity.CustomerName);
            entry.Entity.CustomerEmail = NormalizeForStorage(entry.Entity.CustomerEmail);
            entry.Entity.CustomerPhone = NormalizeNullableSingleLineForStorage(entry.Entity.CustomerPhone);
            entry.Entity.Currency = NormalizeForLookup(entry.Entity.Currency);
            entry.Entity.DiscountCode = NormalizeNullableSingleLineForStorage(entry.Entity.DiscountCode);
            entry.Entity.PaymentIntentId = NormalizeNullableSingleLineForStorage(entry.Entity.PaymentIntentId);
            entry.Entity.ShippingAddressLine1 = NormalizeSingleLineForStorage(entry.Entity.ShippingAddressLine1);
            entry.Entity.ShippingAddressLine2 = NormalizeNullableSingleLineForStorage(entry.Entity.ShippingAddressLine2);
            entry.Entity.ShippingCity = NormalizeSingleLineForStorage(entry.Entity.ShippingCity);
            entry.Entity.ShippingRegion = NormalizeNullableSingleLineForStorage(entry.Entity.ShippingRegion);
            entry.Entity.ShippingPostalCode = NormalizeSingleLineForStorage(entry.Entity.ShippingPostalCode);
            entry.Entity.ShippingCountry = NormalizeSingleLineForStorage(entry.Entity.ShippingCountry);
            entry.Entity.Notes = NormalizeNullableForStorage(entry.Entity.Notes);

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<RefreshSession>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            entry.Entity.CreatedAt = now;
            entry.Entity.LastUsedAt = now;
        }
    }

    private static string NormalizeForStorage(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeSingleLineForStorage(string value)
    {
        return CollapseWhitespace(NormalizeForStorage(value));
    }

    private static string NormalizeForLookup(string value)
    {
        return NormalizeForStorage(value).ToLowerInvariant();
    }

    private static string? NormalizeNullableForStorage(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeNullableSingleLineForStorage(string? value)
    {
        var normalized = NormalizeNullableForStorage(value);
        return normalized is null ? null : CollapseWhitespace(normalized);
    }

    private static string CollapseWhitespace(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : MultiWhitespaceRegex.Replace(value, " ");
    }

    private static List<string> NormalizeStringList(IEnumerable<string>? values)
    {
        return values?
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();
    }
}
