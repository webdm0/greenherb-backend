using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Enter your email or username.")]
    [MinLength(3, ErrorMessage = "Email or username must be at least 3 characters.")]
    [MaxLength(255, ErrorMessage = "Email or username must be 255 characters or fewer.")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [MaxLength(128, ErrorMessage = "Password must be 128 characters or fewer.")]
    public string Password { get; set; } = string.Empty;

    public List<AuthCartItemRequest> CartItems { get; set; } = [];
}
