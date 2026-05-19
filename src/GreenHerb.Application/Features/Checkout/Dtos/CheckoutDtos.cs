namespace GreenHerb.Application.Features.Checkout.Dtos;

public sealed class QuoteCheckoutRequest
{
    public string? DiscountCode { get; set; }
}

public sealed class CreateCheckoutPaymentIntentCommand
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? DiscountCode { get; set; }
    public string ShippingCountry { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string? ShippingAddressLine2 { get; set; }
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string? ShippingRegion { get; set; }
    public string? Notes { get; set; }
}

public sealed class CompleteCheckoutCommand
{
    public int OrderId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
}

public sealed class CheckoutQuoteDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountCode { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class CheckoutPaymentIntentDto
{
    public int OrderId { get; set; }
    public string OrderReference { get; set; } = string.Empty;
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountCode { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class CompletedCheckoutDto
{
    public int OrderId { get; set; }
    public string OrderReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}

public sealed class CheckoutOptions
{
    public const string SectionName = "Stripe";

    public string Currency { get; init; } = "usd";
}

public sealed class CheckoutDiscountResult
{
    public static CheckoutDiscountResult None { get; } = new();

    public string? Code { get; init; }
    public decimal Amount { get; init; }
}

public sealed class CheckoutPaymentIntentRequest
{
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ReceiptEmail { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string? ShippingAddressLine2 { get; set; }
    public string ShippingCity { get; set; } = string.Empty;
    public string? ShippingRegion { get; set; }
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountryCode { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class CheckoutPaymentIntentResult
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class CheckoutPaymentIntentStatus
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
