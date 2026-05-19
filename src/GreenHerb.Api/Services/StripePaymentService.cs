using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;
using Stripe;

namespace GreenHerb.Api.Services;

public sealed class StripePaymentService : IStripePaymentService
{
    private readonly PaymentIntentService _paymentIntentService = new();

    public async Task<CheckoutPaymentIntentResult> CreatePaymentIntentAsync(
        CheckoutPaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        var paymentIntent = await _paymentIntentService.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                ReceiptEmail = request.ReceiptEmail,
                Description = request.Description,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                Metadata = request.Metadata.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value),
                Shipping = new ChargeShippingOptions
                {
                    Name = request.CustomerName,
                    Address = new AddressOptions
                    {
                        Line1 = request.ShippingAddressLine1,
                        Line2 = request.ShippingAddressLine2,
                        City = request.ShippingCity,
                        State = request.ShippingRegion,
                        PostalCode = request.ShippingPostalCode,
                        Country = request.ShippingCountryCode
                    }
                }
            },
            requestOptions: null,
            cancellationToken: cancellationToken);

        return new CheckoutPaymentIntentResult
        {
            PaymentIntentId = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            Status = paymentIntent.Status
        };
    }

    public async Task<CheckoutPaymentIntentStatus> GetPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var paymentIntent = await _paymentIntentService.GetAsync(
            paymentIntentId,
            requestOptions: null,
            cancellationToken: cancellationToken);

        return new CheckoutPaymentIntentStatus
        {
            PaymentIntentId = paymentIntent.Id,
            Status = paymentIntent.Status,
            Amount = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            Metadata = paymentIntent.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value)
        };
    }
}
