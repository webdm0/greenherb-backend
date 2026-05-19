namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class AuthSessionDto
{
    public int SessionId { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; init; }
    public CurrentUserDto User { get; init; } = new();
}
