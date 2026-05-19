namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class CurrentUserDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
}
