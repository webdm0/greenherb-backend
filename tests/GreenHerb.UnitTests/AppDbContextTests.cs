using GreenHerb.Domain.Entities;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GreenHerb.UnitTests;

public sealed class AppDbContextTests
{
    [Fact]
    public void SaveChanges_Normalizes_User_Fields()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var user = new User
        {
            Username = "  Alice  ",
            Email = "  Alice@Example.com ",
            PasswordHash = "hashed-password"
        };

        context.Users.Add(user);
        context.SaveChanges();

        Assert.Equal("Alice", user.Username);
        Assert.Equal("Alice@Example.com", user.Email);
        Assert.Equal("alice", user.NormalizedUsername);
        Assert.Equal("alice@example.com", user.NormalizedEmail);
    }

    [Fact]
    public void SaveChanges_Trims_And_Normalizes_Product_Fields()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var product = new Product
        {
            Name = "  Magnesium Glycinate  ",
            Slug = "  magnesium-glycinate  ",
            Sku = "  GH-MAG-GLY  ",
            Brand = "  GreenHerb  ",
            Category = "  stress-sleep  ",
            Form = "  capsules  ",
            Dosage = "  350 mg  ",
            ServingSize = "  2 capsules  ",
            Ingredients = "  Magnesium glycinate, vegetable capsule.  ",
            HowToUse = "  Take 2 capsules daily with food.  ",
            Warnings = "  Consult a physician if pregnant or nursing.  ",
            ImageUrl = "  /products/magnesium-glycinate.jpg  ",
            Dietary = [" Vegan ", "vegan", " Non-GMO "],
            Rating = 4.6m,
            ReviewCount = 120,
            SoldCount = 450,
            Price = 18.70m,
            QuantityInStock = 50,
            IsFeatured = true
        };

        context.Products.Add(product);
        context.SaveChanges();

        Assert.Equal("Magnesium Glycinate", product.Name);
        Assert.Equal("magnesium-glycinate", product.Slug);
        Assert.Equal("GH-MAG-GLY", product.Sku);
        Assert.Equal("GreenHerb", product.Brand);
        Assert.Equal("stress-sleep", product.Category);
        Assert.Equal("capsules", product.Form);
        Assert.Equal("350 mg", product.Dosage);
        Assert.Equal("2 capsules", product.ServingSize);
        Assert.Equal("Magnesium glycinate, vegetable capsule.", product.Ingredients);
        Assert.Equal("Take 2 capsules daily with food.", product.HowToUse);
        Assert.Equal("Consult a physician if pregnant or nursing.", product.Warnings);
        Assert.Equal("/products/magnesium-glycinate.jpg", product.ImageUrl);
        Assert.Equal(["vegan", "non-gmo"], product.Dietary);
    }

    [Fact]
    public void SaveChanges_Normalizes_ExternalIdentity_Fields()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var user = new User
        {
            Username = "google_user",
            Email = "google@example.com"
        };

        context.Users.Add(user);
        context.ExternalIdentities.Add(new ExternalIdentity
        {
            Provider = "  google  ",
            ProviderUserId = "  1234567890  ",
            Email = "  google@example.com  ",
            DisplayName = "  Google User  ",
            AvatarUrl = "  https://example.com/avatar.png  ",
            EmailVerified = true,
            User = user
        });

        context.SaveChanges();

        var identity = context.ExternalIdentities.Single();
        Assert.Equal("google", identity.Provider);
        Assert.Equal("1234567890", identity.ProviderUserId);
        Assert.Equal("google@example.com", identity.Email);
        Assert.Equal("Google User", identity.DisplayName);
        Assert.Equal("https://example.com/avatar.png", identity.AvatarUrl);
    }
}
