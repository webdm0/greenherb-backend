using GreenHerb.Infrastructure.Configuration;
using GreenHerb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace GreenHerb.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly string _testConnectionString = BuildUniqueTestConnectionString();

    public CustomWebApplicationFactory()
    {
        ApplySharedTestEnvironmentOverrides();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            DotEnvLoader.LoadIfExists(ResolveRepositoryRoot());

            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _testConnectionString,
                ["Jwt:Key"] = "test-jwt-key-with-at-least-32-chars-1234",
                ["Jwt:Issuer"] = "GreenHerb.Tests",
                ["Jwt:Audience"] = "GreenHerb.Tests.Client",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "30",
                ["SessionHint:Key"] = "test-session-hint-key-with-at-least-32-chars-5678",
                ["Stripe:SecretKey"] = "sk_test_placeholder",
                ["Stripe:WebhookSecret"] = "whsec_test_placeholder",
                ["Stripe:Currency"] = "usd",
                ["Cookies:UseSecureAuthCookies"] = "false",
                ["Cookies:UseCrossSiteAuth"] = "false",
                ["AllowedOrigins:0"] = "https://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_testConnectionString);
            });
        });
    }

    public HttpClient CreateApiClient(bool handleCookies = true)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public new Task DisposeAsync()
    {
        CleanupDatabase();
        Dispose();
        return Task.CompletedTask;
    }

    private void CleanupDatabase()
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            using var dbContext = new AppDbContext(options);
            dbContext.Database.EnsureDeleted();
        }
        catch
        {
        }
    }

    private static string BuildUniqueTestConnectionString()
    {
        DotEnvLoader.LoadIfExists(ResolveRepositoryRoot());

        var baseConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            baseConnectionString = "Host=localhost;Port=5432;Database=greenherb_dev;Username=postgres;Password=postgres";
        }

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = $"greenherb_tests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }

    private static void ApplySharedTestEnvironmentOverrides()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "test-jwt-key-with-at-least-32-chars-1234");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "GreenHerb.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "GreenHerb.Tests.Client");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");
        Environment.SetEnvironmentVariable("SessionHint__Key", "test-session-hint-key-with-at-least-32-chars-5678");
        Environment.SetEnvironmentVariable("SessionHint__Issuer", "GreenHerb.Tests");
        Environment.SetEnvironmentVariable("SessionHint__Audience", "GreenHerb.Tests.Client");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_placeholder");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_test_placeholder");
        Environment.SetEnvironmentVariable("Stripe__Currency", "usd");
        Environment.SetEnvironmentVariable("Cookies__UseSecureAuthCookies", "false");
        Environment.SetEnvironmentVariable("Cookies__UseCrossSiteAuth", "false");
        Environment.SetEnvironmentVariable("AllowedOrigins__0", "https://localhost");
        Environment.SetEnvironmentVariable("Authentication__Google__ClientIds__0", string.Empty);
    }

    private static string ResolveRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
