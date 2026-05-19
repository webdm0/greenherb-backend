using GreenHerb.Api.Configuration;
using GreenHerb.Api.Services;
using GreenHerb.Application.Abstractions.Auth;
using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;
using GreenHerb.Application.DependencyInjection;
using GreenHerb.Infrastructure.Configuration;
using GreenHerb.Infrastructure.DependencyInjection;
using GreenHerb.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;

DotEnvLoader.LoadIfExists();

var builder = WebApplication.CreateBuilder(args);

ConfigurePortFromEnvironment(builder);

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is required.");
}

var sessionHintKey = builder.Configuration["SessionHint:Key"];
if (string.IsNullOrWhiteSpace(sessionHintKey))
{
    throw new InvalidOperationException("SessionHint:Key is required.");
}

if (string.Equals(jwtKey, sessionHintKey, StringComparison.Ordinal))
{
    throw new InvalidOperationException("SessionHint:Key must be different from Jwt:Key.");
}

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");
var authCookieOptions = builder.Configuration.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>()
    ?? new AuthCookieOptions();

if (authCookieOptions.UseCrossSiteAuth && !authCookieOptions.UseSecureAuthCookies)
{
    throw new InvalidOperationException("Cookies:UseCrossSiteAuth=true requires Cookies:UseSecureAuthCookies=true.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.Configure<SessionHintOptions>(builder.Configuration.GetSection(SessionHintOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<CheckoutOptions>(builder.Configuration.GetSection(CheckoutOptions.SectionName));
builder.Services.Configure<CatalogSeedOptions>(builder.Configuration.GetSection(CatalogSeedOptions.SectionName));
builder.Services.AddSingleton<ISessionHintService, SessionHintService>();
builder.Services.AddScoped<ICheckoutPaymentGateway, StripePaymentService>();
builder.Services.AddScoped<ICheckoutPromotionService, StripePromotionService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

var stripeOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value;
if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey))
{
    throw new InvalidOperationException("Stripe:SecretKey is required.");
}

Stripe.StripeConfiguration.ApiKey = stripeOptions.SecretKey;

await SeedCatalogIfEnabledAsync(app);

var googleClientIds = app.Configuration
    .GetSection(GoogleAuthOptions.SectionName)
    .Get<GoogleAuthOptions>()?
    .ClientIds
    ?? [];

app.Logger.LogInformation(
    "Configured Google client IDs count: {ClientIdsCount}.",
    googleClientIds.Length);

app.UseStaticFiles();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.MapControllers();

app.Run();

static void ConfigurePortFromEnvironment(WebApplicationBuilder builder)
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (string.IsNullOrWhiteSpace(port))
    {
        return;
    }

    if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort) ||
        parsedPort <= 0)
    {
        throw new InvalidOperationException("PORT must be a positive integer.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

static async Task SeedCatalogIfEnabledAsync(WebApplication app)
{
    var seedOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CatalogSeedOptions>>().Value;
    var shouldSeedOnStartup = seedOptions.OnStartup ?? app.Environment.IsDevelopment();

    if (!shouldSeedOnStartup)
    {
        app.Logger.LogInformation("Catalog seed on startup is disabled.");
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ProductSeeder>();
    await seeder.SeedIfEmptyAsync();
    app.Logger.LogInformation("Catalog seed check completed.");
}

public partial class Program;
