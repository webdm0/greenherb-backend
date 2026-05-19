using System.Globalization;
using System.Security.Cryptography;
using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;
using GreenHerb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DomainCart = GreenHerb.Domain.Entities.Cart;
using DomainOrder = GreenHerb.Domain.Entities.Order;
using DomainOrderItem = GreenHerb.Domain.Entities.OrderItem;

namespace GreenHerb.Application.Features.Checkout.Services;

public sealed class CheckoutService(
    IAppDbContext dbContext,
    ICheckoutPaymentGateway checkoutPaymentGateway,
    ICheckoutPromotionService checkoutPromotionService,
    IOptions<CheckoutOptions> checkoutOptions) : ICheckoutService
{
    private const decimal FreeShippingThreshold = 75m;
    private const decimal FlatShippingAmount = 12m;
    private const string OrderReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int OrderReferenceLength = 8;
    private const int MaxOrderReferenceGenerationAttempts = 5;

    private readonly CheckoutOptions _checkoutOptions = checkoutOptions.Value;

    public async Task<CheckoutQuoteDto> QuoteAsync(int userId, QuoteCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var pricing = await BuildPricingSnapshotAsync(userId, request.DiscountCode, cancellationToken);

        return new CheckoutQuoteDto
        {
            Currency = pricing.Currency,
            Subtotal = pricing.Subtotal,
            DiscountAmount = pricing.Discount.Amount,
            DiscountCode = pricing.Discount.Code,
            ShippingAmount = pricing.ShippingAmount,
            TotalAmount = pricing.TotalAmount
        };
    }

    public async Task<CheckoutPaymentIntentDto> CreatePaymentIntentAsync(
        int userId,
        CreateCheckoutPaymentIntentCommand command,
        CancellationToken cancellationToken = default)
    {
        var pricing = await BuildPricingSnapshotAsync(userId, command.DiscountCode, cancellationToken);
        var order = await CreateOrderAsync(userId, pricing, command, cancellationToken);

        try
        {
            var paymentIntent = await checkoutPaymentGateway.CreatePaymentIntentAsync(
                new CheckoutPaymentIntentRequest
                {
                    Amount = ToMinorUnits(pricing.TotalAmount),
                    Currency = _checkoutOptions.Currency,
                    ReceiptEmail = command.CustomerEmail,
                    Description = $"GreenHerb order {order.OrderReference}",
                    CustomerName = command.CustomerName,
                    ShippingAddressLine1 = command.ShippingAddressLine1,
                    ShippingAddressLine2 = command.ShippingAddressLine2,
                    ShippingCity = command.ShippingCity,
                    ShippingRegion = command.ShippingRegion,
                    ShippingPostalCode = command.ShippingPostalCode,
                    ShippingCountryCode = ToCountryCode(command.ShippingCountry),
                    Metadata = new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id.ToString(),
                        ["orderReference"] = order.OrderReference,
                        ["userId"] = userId.ToString(),
                        ["discountCode"] = pricing.Discount.Code ?? string.Empty,
                        ["discountAmount"] = pricing.Discount.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        ["totalAmount"] = pricing.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                        ["currency"] = pricing.Currency
                    }
                },
                cancellationToken);

            order.PaymentIntentId = paymentIntent.PaymentIntentId;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new CheckoutPaymentIntentDto
            {
                OrderId = order.Id,
                OrderReference = order.OrderReference,
                PaymentIntentId = paymentIntent.PaymentIntentId,
                ClientSecret = paymentIntent.ClientSecret,
                Currency = pricing.Currency,
                Subtotal = pricing.Subtotal,
                DiscountAmount = pricing.Discount.Amount,
                DiscountCode = pricing.Discount.Code,
                ShippingAmount = pricing.ShippingAmount,
                TotalAmount = pricing.TotalAmount
            };
        }
        catch
        {
            dbContext.Orders.Remove(order);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CompletedCheckoutDto> CompleteAsync(int userId, CompleteCheckoutCommand command, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(existingOrder => existingOrder.Items)
            .SingleOrDefaultAsync(
                existingOrder => existingOrder.Id == command.OrderId && existingOrder.UserId == userId,
                cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException();
        }

        var paymentIntent = await checkoutPaymentGateway.GetPaymentIntentAsync(command.PaymentIntentId, cancellationToken);
        var finalizedOrder = await FinalizeSuccessfulPaymentAsync(order, paymentIntent, cancellationToken);

        return new CompletedCheckoutDto
        {
            OrderId = finalizedOrder.Id,
            OrderReference = finalizedOrder.OrderReference,
            Status = finalizedOrder.Status.ToString(),
            PaidAt = finalizedOrder.PaidAt
        };
    }

    public async Task<CompletedCheckoutDto?> HandlePaymentIntentSucceededAsync(
        CheckoutPaymentIntentStatus paymentIntent,
        CancellationToken cancellationToken = default)
    {
        var order = await FindOrderForPaymentAsync(paymentIntent, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var finalizedOrder = await FinalizeSuccessfulPaymentAsync(order, paymentIntent, cancellationToken);

        return new CompletedCheckoutDto
        {
            OrderId = finalizedOrder.Id,
            OrderReference = finalizedOrder.OrderReference,
            Status = finalizedOrder.Status.ToString(),
            PaidAt = finalizedOrder.PaidAt
        };
    }

    private async Task<DomainCart> LoadCartAsync(int userId, CancellationToken cancellationToken)
    {
        return await LoadOptionalCartAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"Cart for user {userId} was not found.");
    }

    private async Task<DomainCart?> LoadOptionalCartAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Carts
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .SingleOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);
    }

    private async Task<DomainOrder?> FindOrderForPaymentAsync(
        CheckoutPaymentIntentStatus paymentIntent,
        CancellationToken cancellationToken)
    {
        if (paymentIntent.Metadata.TryGetValue("orderId", out var metadataOrderId)
            && int.TryParse(metadataOrderId, out var orderId)
            && orderId > 0)
        {
            var orderById = await dbContext.Orders
                .Include(order => order.Items)
                .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

            if (orderById is not null)
            {
                return orderById;
            }
        }

        if (string.IsNullOrWhiteSpace(paymentIntent.PaymentIntentId))
        {
            return null;
        }

        return await dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order => order.PaymentIntentId == paymentIntent.PaymentIntentId,
                cancellationToken);
    }

    private async Task<DomainOrder> FinalizeSuccessfulPaymentAsync(
        DomainOrder order,
        CheckoutPaymentIntentStatus paymentIntent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckoutValidationException("Payment has not succeeded yet.");
        }

        if (!string.Equals(paymentIntent.PaymentIntentId, order.PaymentIntentId, StringComparison.Ordinal))
        {
            throw new CheckoutValidationException("Payment intent does not match the stored order payment.");
        }

        if (paymentIntent.Metadata.TryGetValue("orderId", out var metadataOrderId)
            && !string.Equals(metadataOrderId, order.Id.ToString(), StringComparison.Ordinal))
        {
            throw new CheckoutValidationException("Payment intent does not belong to this order.");
        }

        if (!string.Equals(paymentIntent.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckoutValidationException("Payment currency does not match the order currency.");
        }

        if (paymentIntent.Amount != ToMinorUnits(order.TotalAmount))
        {
            throw new CheckoutValidationException("Paid amount does not match the order total.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return order;
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;

        var cart = await LoadOptionalCartAsync(order.UserId, cancellationToken);
        if (cart is not null && cart.Items.Count > 0)
        {
            dbContext.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return order;
    }

    private static long ToMinorUnits(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static string ToCountryCode(string country)
    {
        return country.Trim().ToUpperInvariant() switch
        {
            "UNITED STATES" => "US",
            "CANADA" => "CA",
            "UNITED KINGDOM" => "GB",
            "GERMANY" => "DE",
            "FRANCE" => "FR",
            "POLAND" => "PL",
            "UKRAINE" => "UA",
            _ => "US"
        };
    }

    private async Task<CheckoutPricingSnapshot> BuildPricingSnapshotAsync(int userId, string? discountCode, CancellationToken cancellationToken)
    {
        var cart = await LoadCartAsync(userId, cancellationToken);

        if (cart.Items.Count == 0)
        {
            throw new CheckoutValidationException("Cart is empty.");
        }

        var subtotal = cart.Items.Sum(item => item.Product.Price * item.Quantity);
        var appliedDiscount = await checkoutPromotionService.ResolveDiscountAsync(
            discountCode,
            userId,
            subtotal,
            _checkoutOptions.Currency,
            cancellationToken);
        var discountedSubtotal = Math.Max(0m, subtotal - appliedDiscount.Amount);
        var shippingAmount = discountedSubtotal >= FreeShippingThreshold ? 0m : FlatShippingAmount;
        var totalAmount = discountedSubtotal + shippingAmount;

        return new CheckoutPricingSnapshot
        {
            Cart = cart,
            Currency = _checkoutOptions.Currency,
            Discount = appliedDiscount,
            ShippingAmount = shippingAmount,
            Subtotal = subtotal,
            TotalAmount = totalAmount
        };
    }

    private async Task<DomainOrder> CreateOrderAsync(
        int userId,
        CheckoutPricingSnapshot pricing,
        CreateCheckoutPaymentIntentCommand command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxOrderReferenceGenerationAttempts; attempt++)
        {
            var order = new DomainOrder
            {
                OrderReference = GenerateOrderReference(),
                UserId = userId,
                Status = OrderStatus.Pending,
                Currency = pricing.Currency,
                SubtotalAmount = pricing.Subtotal,
                DiscountAmount = pricing.Discount.Amount,
                DiscountCode = pricing.Discount.Code,
                TotalAmount = pricing.TotalAmount,
                ShippingAmount = pricing.ShippingAmount,
                CustomerName = command.CustomerName,
                CustomerEmail = command.CustomerEmail,
                CustomerPhone = command.CustomerPhone,
                ShippingCountry = command.ShippingCountry,
                ShippingCity = command.ShippingCity,
                ShippingAddressLine1 = command.ShippingAddressLine1,
                ShippingAddressLine2 = command.ShippingAddressLine2,
                ShippingPostalCode = command.ShippingPostalCode,
                ShippingRegion = command.ShippingRegion,
                Notes = command.Notes,
                Items = pricing.Cart.Items
                    .Select(item => new DomainOrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        ProductSlug = item.Product.Slug,
                        ProductSku = item.Product.Sku,
                        ProductImageUrl = item.Product.ImageUrl,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    })
                    .ToList()
            };

            dbContext.Orders.Add(order);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return order;
            }
            catch (DbUpdateException exception) when (IsOrderReferenceConflict(exception))
            {
                dbContext.Orders.Remove(order);
            }
        }

        throw new InvalidOperationException("Unable to create a unique order reference.");
    }

    private static string GenerateOrderReference()
    {
        Span<char> buffer = stackalloc char[OrderReferenceLength];

        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = OrderReferenceAlphabet[RandomNumberGenerator.GetInt32(OrderReferenceAlphabet.Length)];
        }

        return new string(buffer);
    }

    private static bool IsOrderReferenceConflict(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("IX_Orders_OrderReference", StringComparison.Ordinal) == true;
    }

    private sealed class CheckoutPricingSnapshot
    {
        public required DomainCart Cart { get; init; }
        public string Currency { get; init; } = string.Empty;
        public required CheckoutDiscountResult Discount { get; init; }
        public decimal ShippingAmount { get; init; }
        public decimal Subtotal { get; init; }
        public decimal TotalAmount { get; init; }
    }
}
