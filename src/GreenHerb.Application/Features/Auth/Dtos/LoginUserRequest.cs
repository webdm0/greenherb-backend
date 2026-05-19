namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class LoginUserRequest
{
    public string Identifier { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public List<AuthCartItemInput> CartItems { get; init; } = [];
}
