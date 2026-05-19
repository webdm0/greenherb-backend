using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Domain.Entities;

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    [Required]
    [MaxLength(160)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string ProductSku { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProductImageUrl { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
