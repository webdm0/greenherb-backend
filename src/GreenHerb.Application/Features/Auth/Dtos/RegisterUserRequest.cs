namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class RegisterUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public List<AuthCartItemInput> CartItems { get; init; } = [];
}
