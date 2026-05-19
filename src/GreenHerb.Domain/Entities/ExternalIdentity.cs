using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Domain.Entities;

public sealed class ExternalIdentity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(40)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string ProviderUserId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    [MaxLength(255)]
    public string? DisplayName { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
