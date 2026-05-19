using GreenHerb.Application.Features.Checkout.Dtos;

namespace GreenHerb.Application.Features.Checkout.Interfaces;

public interface ICheckoutService
{
    Task<CheckoutQuoteDto> QuoteAsync(int userId, QuoteCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutPaymentIntentDto> CreatePaymentIntentAsync(int userId, CreateCheckoutPaymentIntentCommand command, CancellationToken cancellationToken = default);
    Task<CompletedCheckoutDto> CompleteAsync(int userId, CompleteCheckoutCommand command, CancellationToken cancellationToken = default);
    Task<CompletedCheckoutDto?> HandlePaymentIntentSucceededAsync(CheckoutPaymentIntentStatus paymentIntent, CancellationToken cancellationToken = default);
}
