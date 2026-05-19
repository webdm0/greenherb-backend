namespace GreenHerb.Api.Contracts.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public AuthenticatedUserResponse User { get; init; } = new();
}
