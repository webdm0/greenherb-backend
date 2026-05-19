using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Api.Contracts.Auth;

public sealed class GoogleLoginRequest
{
    [Required(ErrorMessage = "Google ID token is required.")]
    [MinLength(20, ErrorMessage = "Google ID token is invalid.")]
    public string IdToken { get; set; } = string.Empty;

    public List<AuthCartItemRequest> CartItems { get; set; } = [];
}
