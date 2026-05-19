using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Checkout.Dtos;
using Stripe;

namespace GreenHerb.Api.Services;

public sealed class StripePromotionService : IStripePromotionService
{
    private readonly PromotionCodeService _promotionCodeService = new();

    public async Task<CheckoutDiscountResult> ResolveDiscountAsync(
        string? discountCode,
        int userId,
        decimal subtotal,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = discountCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return CheckoutDiscountResult.None;
        }

        var options = new PromotionCodeListOptions
        {
            Code = normalizedCode,
            Active = true,
            Limit = 1
        };
        options.AddExpand("data.promotion.coupon");

        try
        {
            var promoCodes = await _promotionCodeService.ListAsync(options, cancellationToken: cancellationToken);
            var promoCode = promoCodes.FirstOrDefault();
            if (promoCode is null)
            {
                throw new CheckoutValidationException("Promo code is invalid.");
            }

            EnsurePromotionCodeCanBeApplied(promoCode, subtotal, currency);

            var coupon = promoCode.Promotion?.Coupon
                ?? throw new CheckoutValidationException("Promo code coupon data is unavailable.");

            var discountAmount = CalculateDiscountAmount(coupon, subtotal);
            if (discountAmount <= 0m)
            {
                throw new CheckoutValidationException("Promo code does not apply to this order.");
            }

            return new CheckoutDiscountResult
            {
                Code = normalizedCode,
                Amount = Math.Min(subtotal, discountAmount)
            };
        }
        catch (StripeException)
        {
            throw new CheckoutValidationException("Error validating promo code.");
        }
    }

    private void EnsurePromotionCodeCanBeApplied(PromotionCode promoCode, decimal subtotal, string currency)
    {
        var coupon = promoCode.Promotion?.Coupon;
        if (coupon is null)
        {
            throw new CheckoutValidationException("Promo code coupon data is unavailable.");
        }

        if (!coupon.Valid)
        {
            throw new CheckoutValidationException("Promo code is no longer available.");
        }

        if (coupon.AmountOff.HasValue
            && !string.IsNullOrWhiteSpace(coupon.Currency)
            && !string.Equals(coupon.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckoutValidationException("Promo code does not support the current order currency.");
        }

        if (promoCode.ExpiresAt.HasValue && promoCode.ExpiresAt.Value <= DateTime.UtcNow)
        {
            throw new CheckoutValidationException("Promo code has expired.");
        }

        if (promoCode.MaxRedemptions.HasValue && promoCode.TimesRedeemed >= promoCode.MaxRedemptions.Value)
        {
            throw new CheckoutValidationException("Promo code redemption limit has been reached.");
        }

        var restrictions = promoCode.Restrictions;
        if (restrictions is null)
        {
            return;
        }

        var expectedCurrency = currency.Trim().ToLowerInvariant();
        var minimumAmountInMinorUnits = restrictions.MinimumAmount;
        if (!string.IsNullOrWhiteSpace(restrictions.MinimumAmountCurrency)
            && !string.Equals(restrictions.MinimumAmountCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            minimumAmountInMinorUnits = null;
        }

        if (minimumAmountInMinorUnits.HasValue && ToMinorUnits(subtotal) < minimumAmountInMinorUnits.Value)
        {
            throw new CheckoutValidationException("Order does not meet the promo code minimum amount.");
        }
    }

    private static decimal CalculateDiscountAmount(Coupon coupon, decimal subtotal)
    {
        if (coupon.PercentOff.HasValue)
        {
            return decimal.Round(
                subtotal * (coupon.PercentOff.Value / 100m),
                2,
                MidpointRounding.AwayFromZero);
        }

        if (coupon.AmountOff.HasValue)
        {
            return decimal.Round(
                coupon.AmountOff.Value / 100m,
                2,
                MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static long ToMinorUnits(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }
}
