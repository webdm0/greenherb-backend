using System.ComponentModel.DataAnnotations;
using GreenHerb.Domain.Enums;
using GreenHerb.Domain.Validation;

namespace GreenHerb.Domain.Entities;

public sealed class Order
{
    public int Id { get; set; }
    public string OrderReference { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string Currency { get; set; } = "usd";
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountCode { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public string? PaymentIntentId { get; set; }

    [Required]
    [MinLength(OrderValidation.CustomerNameMinLength)]
    [MaxLength(OrderValidation.CustomerNameMaxLength)]
    [RegularExpression(OrderValidation.PersonNamePattern, ErrorMessage = OrderValidation.CustomerNameMessage)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(OrderValidation.CustomerEmailMaxLength)]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(OrderValidation.CustomerPhoneMaxLength)]
    [RegularExpression(OrderValidation.PhonePattern, ErrorMessage = OrderValidation.CustomerPhoneMessage)]
    public string? CustomerPhone { get; set; }

    [Required]
    [MaxLength(OrderValidation.ShippingAddressMaxLength)]
    [RegularExpression(OrderValidation.AddressPattern, ErrorMessage = OrderValidation.ShippingAddressMessage)]
    public string ShippingAddressLine1 { get; set; } = string.Empty;

    [MaxLength(OrderValidation.ShippingAddressMaxLength)]
    [RegularExpression(OrderValidation.AddressPattern, ErrorMessage = OrderValidation.ShippingAddressMessage)]
    public string? ShippingAddressLine2 { get; set; }

    [Required]
    [MaxLength(OrderValidation.ShippingLocationMaxLength)]
    [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
    public string ShippingCity { get; set; } = string.Empty;

    [MaxLength(OrderValidation.ShippingLocationMaxLength)]
    [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
    public string? ShippingRegion { get; set; }

    [Required]
    [MaxLength(OrderValidation.ShippingPostalCodeMaxLength)]
    [RegularExpression(OrderValidation.PostalCodePattern, ErrorMessage = OrderValidation.ShippingPostalCodeMessage)]
    public string ShippingPostalCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(OrderValidation.ShippingLocationMaxLength)]
    [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
    public string ShippingCountry { get; set; } = string.Empty;

    [MaxLength(OrderValidation.NotesMaxLength)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
