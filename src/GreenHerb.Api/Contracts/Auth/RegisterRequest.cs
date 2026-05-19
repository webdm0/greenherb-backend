using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Enter a username.")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
    [MaxLength(40, ErrorMessage = "Username must be 40 characters or fewer.")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Use only English letters, numbers, underscores, and hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(255, ErrorMessage = "Email address must be 255 characters or fewer.")]
    [RegularExpression(@"^[\x21-\x7E]+$", ErrorMessage = "Use an email address with English letters, numbers, and standard symbols only.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a password.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128, ErrorMessage = "Password must be 128 characters or fewer.")]
    public string Password { get; set; } = string.Empty;

    public List<AuthCartItemRequest> CartItems { get; set; } = [];
}
