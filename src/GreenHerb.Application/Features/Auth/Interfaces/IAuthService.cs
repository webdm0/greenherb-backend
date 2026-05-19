using GreenHerb.Application.Features.Auth.Dtos;

namespace GreenHerb.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthSessionDto> RegisterAsync(
        RegisterUserRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthSessionDto> LoginAsync(
        LoginUserRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthSessionDto> LoginWithGoogleAsync(
        GoogleAuthRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthSessionDto?> RefreshSessionAsync(
        string? refreshToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedSessionDto?> GetSessionAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default);

    Task<CurrentUserDto?> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
}
