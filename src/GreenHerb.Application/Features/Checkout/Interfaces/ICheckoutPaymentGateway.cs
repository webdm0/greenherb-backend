using GreenHerb.Application.Features.Checkout.Dtos;

namespace GreenHerb.Application.Features.Checkout.Interfaces;

public interface ICheckoutPaymentGateway
{
    Task<CheckoutPaymentIntentResult> CreatePaymentIntentAsync(CheckoutPaymentIntentRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutPaymentIntentStatus> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);
}
