using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Domain.Entities;

public sealed class RefreshSession
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    [MaxLength(128)]
    public string? IpAddress { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
