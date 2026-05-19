using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Domain.Entities;

public sealed class User
{
    public int Id { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(40)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string NormalizedUsername { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Cart? Cart { get; set; }
    public List<RefreshSession> Sessions { get; set; } = [];
    public List<ExternalIdentity> ExternalIdentities { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}
