namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class GoogleAuthRequest
{
    public string IdToken { get; set; } = string.Empty;
    public List<AuthCartItemInput> CartItems { get; init; } = [];
}
