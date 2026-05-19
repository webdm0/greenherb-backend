using GreenHerb.Application.Features.Checkout.Dtos;

namespace GreenHerb.Application.Features.Checkout.Interfaces;

public interface ICheckoutPromotionService
{
    Task<CheckoutDiscountResult> ResolveDiscountAsync(
        string? discountCode,
        int userId,
        decimal subtotal,
        string currency,
        CancellationToken cancellationToken = default);
}
