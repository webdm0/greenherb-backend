using System.Text;
using GreenHerb.Api.Configuration;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace GreenHerb.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/stripe/webhook")]
public sealed class StripeWebhookController(
    ICheckoutService checkoutService,
    IOptions<StripeOptions> stripeOptions,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    private const string PaymentIntentSucceededEventType = "payment_intent.succeeded";
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            logger.LogError("Stripe webhook secret is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return BadRequest();
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _stripeOptions.WebhookSecret,
                300,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            logger.LogWarning(exception, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        if (stripeEvent.Type == PaymentIntentSucceededEventType && stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            try
            {
                var result = await checkoutService.HandlePaymentIntentSucceededAsync(
                    new CheckoutPaymentIntentStatus
                    {
                        PaymentIntentId = paymentIntent.Id,
                        Status = paymentIntent.Status,
                        Amount = paymentIntent.Amount,
                        Currency = paymentIntent.Currency,
                        Metadata = paymentIntent.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value)
                    },
                    cancellationToken);

                if (result is null)
                {
                    logger.LogWarning(
                        "Stripe webhook payment intent succeeded but no matching order was found. PaymentIntentId: {PaymentIntentId}",
                        paymentIntent.Id);
                }
            }
            catch (CheckoutValidationException exception)
            {
                logger.LogWarning(
                    exception,
                    "Stripe webhook payment validation failed for PaymentIntentId {PaymentIntentId}.",
                    paymentIntent.Id);
                return BadRequest();
            }
        }

        return Ok();
    }
}
