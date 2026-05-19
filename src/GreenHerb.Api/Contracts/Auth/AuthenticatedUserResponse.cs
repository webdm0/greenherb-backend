namespace GreenHerb.Api.Contracts.Auth;

public sealed class AuthenticatedUserResponse
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
}
