namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class AuthenticatedSessionDto
{
    public int SessionId { get; init; }
    public CurrentUserDto User { get; init; } = new();
}
