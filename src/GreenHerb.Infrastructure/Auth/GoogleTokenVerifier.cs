using GreenHerb.Application.Abstractions.Auth;
using GreenHerb.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreenHerb.Infrastructure.Auth;

public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private static readonly string[] ValidIssuers =
    [
        "accounts.google.com",
        "https://accounts.google.com"
    ];

    private static readonly Uri DiscoveryDocumentUri = new("https://accounts.google.com/.well-known/openid-configuration");
    private static readonly TimeSpan MetadataCacheLifetime = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleTokenVerifier> _logger;
    private readonly GoogleAuthOptions _options;
    private readonly SemaphoreSlim _metadataLock = new(1, 1);

    private IReadOnlyList<SecurityKey> _cachedSigningKeys = [];
    private DateTime _metadataExpiresAtUtc = DateTime.MinValue;

    public GoogleTokenVerifier(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleAuthOptions> options,
        ILogger<GoogleTokenVerifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleTokenVerificationResult> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || _options.ClientIds.Length == 0)
        {
            _logger.LogWarning(
                "Google ID token validation skipped. Token empty: {IsTokenEmpty}. Configured client IDs: {ClientIdsCount}.",
                string.IsNullOrWhiteSpace(idToken),
                _options.ClientIds.Length);
            return GoogleTokenVerificationResult.Failure(
                string.IsNullOrWhiteSpace(idToken)
                    ? "Google ID token was empty."
                    : "No Google client IDs are configured on the backend.");
        }

        IReadOnlyList<SecurityKey> signingKeys;
        try
        {
            signingKeys = await GetSigningKeysAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load Google signing keys.");
            return GoogleTokenVerificationResult.Failure(
                $"Failed to load Google signing keys: {exception.Message}");
        }

        if (signingKeys.Count == 0)
        {
            _logger.LogWarning(
                "Google ID token validation skipped because no signing keys were loaded. Configured client IDs: {ClientIds}.",
                _options.ClientIds);
            return GoogleTokenVerificationResult.Failure(
                "The backend loaded zero Google signing keys.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ValidIssuers,
            ValidateAudience = true,
            ValidAudiences = _options.ClientIds.Where(clientId => !string.IsNullOrWhiteSpace(clientId)),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(idToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger.LogWarning("Google ID token validation failed because the validated token was not a JWT.");
                return GoogleTokenVerificationResult.Failure(
                    "Google returned a token that was not recognized as a JWT.");
            }

            var subject = GetClaimValue(principal, jwtToken, JwtRegisteredClaimNames.Sub);
            var email = GetClaimValue(principal, jwtToken, JwtRegisteredClaimNames.Email);
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning(
                    "Google ID token validation failed because required claims were missing. Subject present: {HasSubject}. Email present: {HasEmail}.",
                    !string.IsNullOrWhiteSpace(subject),
                    !string.IsNullOrWhiteSpace(email));
                return GoogleTokenVerificationResult.Failure(
                    "The Google ID token is missing required claims.");
            }

            return GoogleTokenVerificationResult.Success(new GoogleIdentityInfo
            {
                Subject = subject,
                Email = email,
                EmailVerified = ParseBooleanClaim(principal, jwtToken, "email_verified"),
                Name = GetClaimValue(principal, jwtToken, "name"),
                PictureUrl = GetClaimValue(principal, jwtToken, "picture")
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Google ID token validation failed. Configured client IDs: {ClientIds}.",
                _options.ClientIds);
            return GoogleTokenVerificationResult.Failure(exception.Message);
        }
    }

    private async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (_cachedSigningKeys.Count > 0 && _metadataExpiresAtUtc > DateTime.UtcNow)
        {
            return _cachedSigningKeys;
        }

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedSigningKeys.Count > 0 && _metadataExpiresAtUtc > DateTime.UtcNow)
            {
                return _cachedSigningKeys;
            }

            var httpClient = _httpClientFactory.CreateClient();
            var discoveryDocument = await GetDiscoveryDocumentAsync(httpClient, cancellationToken);
            if (discoveryDocument?.JwksUri is null)
            {
                _logger.LogWarning("Google discovery document did not contain a JWKS URI.");
                _cachedSigningKeys = [];
                _metadataExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
                return _cachedSigningKeys;
            }

            var signingKeys = await GetSigningKeysAsync(httpClient, discoveryDocument.JwksUri, cancellationToken);
            _logger.LogInformation(
                "Loaded {SigningKeysCount} Google signing keys for client IDs: {ClientIds}.",
                signingKeys.Count,
                _options.ClientIds);
            _cachedSigningKeys = signingKeys;
            _metadataExpiresAtUtc = DateTime.UtcNow.Add(MetadataCacheLifetime);

            return _cachedSigningKeys;
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private static async Task<GoogleDiscoveryDocument?> GetDiscoveryDocumentAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(DiscoveryDocumentUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GoogleDiscoveryDocument>(stream, cancellationToken: cancellationToken);
    }

    private static async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(
        HttpClient httpClient,
        Uri jwksUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(jwksUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var jwks = await JsonSerializer.DeserializeAsync<GoogleJwksDocument>(stream, cancellationToken: cancellationToken);
        if (jwks?.Keys is null)
        {
            return [];
        }

        var keys = new List<SecurityKey>();
        foreach (var key in jwks.Keys)
        {
            if (!string.Equals(key.Kty, "RSA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(key.N) ||
                string.IsNullOrWhiteSpace(key.E))
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlEncoder.DecodeBytes(key.N),
                Exponent = Base64UrlEncoder.DecodeBytes(key.E)
            });

            keys.Add(new RsaSecurityKey(rsa)
            {
                KeyId = key.Kid
            });
        }

        return keys;
    }

    private static bool ParseBooleanClaim(
        ClaimsPrincipal principal,
        JwtSecurityToken jwtToken,
        string claimType)
    {
        var claimValue = GetClaimValue(principal, jwtToken, claimType);
        return bool.TryParse(claimValue, out var parsed) && parsed;
    }

    private static string? GetClaimValue(
        ClaimsPrincipal principal,
        JwtSecurityToken jwtToken,
        string claimType)
    {
        if (jwtToken.Payload.TryGetValue(claimType, out var payloadValue) && payloadValue is not null)
        {
            return payloadValue.ToString();
        }

        return principal.FindFirst(claimType)?.Value;
    }

    private sealed class GoogleDiscoveryDocument
    {
        [JsonPropertyName("jwks_uri")]
        public Uri? JwksUri { get; init; }
    }

    private sealed class GoogleJwksDocument
    {
        [JsonPropertyName("keys")]
        public List<GoogleJwk>? Keys { get; init; }
    }

    private sealed class GoogleJwk
    {
        [JsonPropertyName("kid")]
        public string? Kid { get; init; }

        [JsonPropertyName("kty")]
        public string? Kty { get; init; }

        [JsonPropertyName("n")]
        public string? N { get; init; }

        [JsonPropertyName("e")]
        public string? E { get; init; }
    }
}
