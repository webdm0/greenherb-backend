using GreenHerb.Application.Abstractions.Auth;
using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Auth.Dtos;
using GreenHerb.Application.Features.Auth.Interfaces;
using GreenHerb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using DomainCart = GreenHerb.Domain.Entities.Cart;
using DomainCartItem = GreenHerb.Domain.Entities.CartItem;

namespace GreenHerb.Application.Features.Auth.Services;

public sealed class AuthService : IAuthService
{
    private const string GoogleProvider = "google";

    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenProvider _jwtTokenProvider;
    private readonly IGoogleTokenVerifier _googleTokenVerifier;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IAppDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenProvider jwtTokenProvider,
        IGoogleTokenVerifier googleTokenVerifier,
        IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenProvider = jwtTokenProvider;
        _googleTokenVerifier = googleTokenVerifier;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthSessionDto> RegisterAsync(
        RegisterUserRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsernameLookup = NormalizeForLookup(request.Username);
        var normalizedEmailLookup = NormalizeForLookup(request.Email);

        if (await _context.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsernameLookup, cancellationToken))
        {
            throw new UsernameAlreadyTakenException();
        }

        if (await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmailLookup, cancellationToken))
        {
            throw new EmailAlreadyTakenException();
        }

        var user = new User
        {
            Username = NormalizeForStorage(request.Username),
            Email = NormalizeForStorage(request.Email),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Cart = new DomainCart()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        await MergeGuestCartAsync(user.Id, request.CartItems, cancellationToken);

        return await CreateSessionAsync(user, userAgent, ipAddress, cancellationToken);
    }

    public async Task<AuthSessionDto> LoginAsync(
        LoginUserRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = NormalizeForLookup(request.Identifier);
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.NormalizedEmail == normalizedIdentifier || u.NormalizedUsername == normalizedIdentifier,
            cancellationToken);

        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        await MergeGuestCartAsync(user.Id, request.CartItems, cancellationToken);

        return await CreateSessionAsync(user, userAgent, ipAddress, cancellationToken);
    }

    public async Task<AuthSessionDto> LoginWithGoogleAsync(
        GoogleAuthRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var verificationResult = await _googleTokenVerifier.VerifyIdTokenAsync(request.IdToken, cancellationToken);
        if (!verificationResult.IsSuccess || verificationResult.Identity is null)
        {
            throw new InvalidGoogleTokenException(verificationResult.FailureReason);
        }

        var googleIdentity = verificationResult.Identity;

        if (!googleIdentity.EmailVerified)
        {
            throw new GoogleEmailNotVerifiedException();
        }

        var normalizedEmail = NormalizeForLookup(googleIdentity.Email);
        var externalIdentity = await _context.ExternalIdentities
            .Include(identity => identity.User)
            .FirstOrDefaultAsync(
                identity => identity.Provider == GoogleProvider && identity.ProviderUserId == googleIdentity.Subject,
                cancellationToken);

        if (externalIdentity is not null)
        {
            UpdateExternalIdentity(externalIdentity, googleIdentity);
            await _context.SaveChangesAsync(cancellationToken);
            await MergeGuestCartAsync(externalIdentity.User.Id, request.CartItems, cancellationToken);
            return await CreateSessionAsync(externalIdentity.User, userAgent, ipAddress, cancellationToken);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Username = await GenerateUniqueUsernameAsync(googleIdentity, cancellationToken),
                Email = NormalizeForStorage(googleIdentity.Email),
                PasswordHash = null,
                Cart = new DomainCart()
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }

        externalIdentity = new ExternalIdentity
        {
            Provider = GoogleProvider,
            ProviderUserId = googleIdentity.Subject,
            Email = NormalizeForStorage(googleIdentity.Email),
            EmailVerified = googleIdentity.EmailVerified,
            DisplayName = NormalizeOptionalValue(googleIdentity.Name, 255),
            AvatarUrl = NormalizeOptionalValue(googleIdentity.PictureUrl, 500),
            UserId = user.Id
        };

        _context.ExternalIdentities.Add(externalIdentity);
        await _context.SaveChangesAsync(cancellationToken);

        await MergeGuestCartAsync(user.Id, request.CartItems, cancellationToken);

        return await CreateSessionAsync(user, userAgent, ipAddress, cancellationToken);
    }

    public async Task<AuthSessionDto?> RefreshSessionAsync(
        string? refreshToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshHash = HashRefreshToken(refreshToken);
        var session = await _context.RefreshSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(
                s => s.TokenHash == refreshHash && s.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (session is null)
        {
            return null;
        }

        var newRefreshToken = GenerateRefreshToken();
        session.TokenHash = HashRefreshToken(newRefreshToken);
        session.ExpiresAt = GetRefreshTokenExpiresAtUtc();
        session.LastUsedAt = DateTime.UtcNow;
        session.UserAgent = NormalizeOptionalValue(userAgent, 512);
        session.IpAddress = NormalizeOptionalValue(ipAddress, 128);

        await _context.SaveChangesAsync(cancellationToken);

        return BuildAuthSession(session.User, newRefreshToken, session.ExpiresAt, session.Id);
    }

    public async Task<AuthenticatedSessionDto?> GetSessionAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshHash = HashRefreshToken(refreshToken);
        var session = await _context.RefreshSessions
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TokenHash == refreshHash && s.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        return session is null
            ? null
            : new AuthenticatedSessionDto
            {
                SessionId = session.Id,
                User = MapUser(session.User)
            };
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : MapUser(user);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var refreshHash = HashRefreshToken(refreshToken);
        var session = await _context.RefreshSessions
            .FirstOrDefaultAsync(s => s.TokenHash == refreshHash, cancellationToken);

        if (session is null)
        {
            return;
        }

        _context.RefreshSessions.Remove(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthSessionDto> CreateSessionAsync(
        User user,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var maxSessions = _jwtOptions.MaxSessions > 0 ? _jwtOptions.MaxSessions : 5;
        var activeSessions = await _context.RefreshSessions
            .Where(s => s.UserId == user.Id)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        while (activeSessions.Count >= maxSessions)
        {
            _context.RefreshSessions.Remove(activeSessions[0]);
            activeSessions.RemoveAt(0);
        }

        var refreshToken = GenerateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = GetRefreshTokenExpiresAtUtc(),
            UserAgent = NormalizeOptionalValue(userAgent, 512),
            IpAddress = NormalizeOptionalValue(ipAddress, 128)
        };

        _context.RefreshSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return BuildAuthSession(user, refreshToken, session.ExpiresAt, session.Id);
    }

    private async Task MergeGuestCartAsync(
        int userId,
        IReadOnlyCollection<AuthCartItemInput>? cartItems,
        CancellationToken cancellationToken)
    {
        var itemsToMerge = (cartItems ?? [])
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new AuthCartItemInput
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        if (itemsToMerge.Count == 0)
        {
            return;
        }

        var cart = await _context.Carts
            .Include(existingCart => existingCart.Items)
            .SingleOrDefaultAsync(existingCart => existingCart.UserId == userId, cancellationToken);

        if (cart is null)
        {
            cart = new DomainCart
            {
                UserId = userId
            };

            _context.Carts.Add(cart);
        }

        var productIds = itemsToMerge.Select(item => item.ProductId).ToArray();
        var products = await _context.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var item in itemsToMerge)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var existingItem = cart.Items.SingleOrDefault(cartItem => cartItem.ProductId == item.ProductId);
            if (existingItem is null)
            {
                cart.Items.Add(new DomainCartItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });

                continue;
            }

            existingItem.Quantity += item.Quantity;
            existingItem.UnitPrice = product.Price;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private AuthSessionDto BuildAuthSession(
        User user,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc,
        int sessionId)
    {
        return new AuthSessionDto
        {
            SessionId = sessionId,
            AccessToken = _jwtTokenProvider.GenerateAccessToken(user),
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            User = MapUser(user)
        };
    }

    private CurrentUserDto MapUser(User user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsAdmin = user.IsAdmin
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(
        GoogleIdentityInfo googleIdentity,
        CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            BuildUsernameCandidateFromEmail(googleIdentity.Email),
            BuildUsernameCandidateFromName(googleIdentity.Name),
            $"{GoogleProvider}_{googleIdentity.Subject}"
        };

        foreach (var candidate in candidates)
        {
            var username = NormalizeUsernameCandidate(candidate);
            if (await IsUsernameAvailableAsync(username, cancellationToken))
            {
                return username;
            }
        }

        var baseUsername = NormalizeUsernameCandidate(BuildUsernameCandidateFromEmail(googleIdentity.Email));
        for (var suffix = 1; suffix <= 5000; suffix++)
        {
            var candidate = AppendNumericSuffix(baseUsername, suffix);
            if (await IsUsernameAvailableAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return NormalizeUsernameCandidate($"{GoogleProvider}_{Guid.NewGuid():N}");
    }

    private async Task<bool> IsUsernameAvailableAsync(string username, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeForLookup(username);
        return !await _context.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    private static DateTime GetRefreshTokenExpiresAtUtc(int refreshTokenDays)
    {
        return DateTime.UtcNow.AddDays(refreshTokenDays > 0 ? refreshTokenDays : 7);
    }

    private DateTime GetRefreshTokenExpiresAtUtc()
    {
        return GetRefreshTokenExpiresAtUtc(_jwtOptions.RefreshTokenDays);
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private static void UpdateExternalIdentity(ExternalIdentity externalIdentity, GoogleIdentityInfo googleIdentity)
    {
        externalIdentity.Email = NormalizeForStorage(googleIdentity.Email);
        externalIdentity.EmailVerified = googleIdentity.EmailVerified;
        externalIdentity.DisplayName = NormalizeOptionalValue(googleIdentity.Name, 255);
        externalIdentity.AvatarUrl = NormalizeOptionalValue(googleIdentity.PictureUrl, 500);
    }

    private static string HashRefreshToken(string token)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    private static string BuildUsernameCandidateFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static string? BuildUsernameCandidateFromName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string NormalizeForStorage(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeForLookup(string value)
    {
        return NormalizeForStorage(value).ToLowerInvariant();
    }

    private static string NormalizeUsernameCandidate(string? value)
    {
        const int minLength = 3;
        const int maxLength = 40;

        var characters = (value ?? string.Empty)
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_')
            .ToArray();

        var normalized = new string(characters).Trim('_', '-');
        if (normalized.Length == 0)
        {
            normalized = "user";
        }

        if (normalized.Length < minLength)
        {
            normalized = normalized.PadRight(minLength, '0');
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string AppendNumericSuffix(string username, int suffix)
    {
        const int maxLength = 40;

        var suffixText = suffix.ToString();
        var baseLength = Math.Max(1, maxLength - suffixText.Length - 1);
        var prefix = username.Length <= baseLength ? username : username[..baseLength];
        return $"{prefix}_{suffixText}";
    }

    private static string? NormalizeOptionalValue(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
