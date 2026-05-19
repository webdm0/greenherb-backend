using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Domain.Entities;

public sealed class Product
{
    public int Id { get; set; }

    [Required]
    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Brand { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Form { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Dosage { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ServingSize { get; set; } = string.Empty;

    public int? CountInPack { get; set; }

    public int? ServingsPerContainer { get; set; }

    [MaxLength(280)]
    public string ShortDescription { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Ingredients { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string HowToUse { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Warnings { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int QuantityInStock { get; set; }

    public List<string> Dietary { get; set; } = [];
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public int SoldCount { get; set; }
    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CartItem> CartItems { get; set; } = [];
    public List<OrderItem> OrderItems { get; set; } = [];
}
