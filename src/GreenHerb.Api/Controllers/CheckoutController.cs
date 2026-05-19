using System.ComponentModel.DataAnnotations;
using GreenHerb.Api.Extensions;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;
using GreenHerb.Domain.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GreenHerb.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class CheckoutController(
    ICheckoutService checkoutService,
    ILogger<CheckoutController> logger) : ControllerBase
{
    [HttpPost("quote")]
    public async Task<IActionResult> Quote([FromBody] CheckoutQuoteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await checkoutService.QuoteAsync(
                GetRequiredUserId(),
                new QuoteCheckoutRequest
                {
                    DiscountCode = request.DiscountCode
                },
                cancellationToken);

            return Ok(result);
        }
        catch (CheckoutValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent(
        [FromBody] CreateCheckoutPaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        try
        {
            var result = await checkoutService.CreatePaymentIntentAsync(
                userId,
                new CreateCheckoutPaymentIntentCommand
                {
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    DiscountCode = request.DiscountCode,
                    ShippingCountry = request.ShippingCountry,
                    ShippingCity = request.ShippingCity,
                    ShippingAddressLine1 = request.ShippingAddressLine1,
                    ShippingAddressLine2 = request.ShippingAddressLine2,
                    ShippingPostalCode = request.ShippingPostalCode,
                    ShippingRegion = request.ShippingRegion,
                    Notes = request.Notes
                },
                cancellationToken);

            return Ok(result);
        }
        catch (CheckoutValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create checkout payment intent for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to create payment intent." });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteCheckoutRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await checkoutService.CompleteAsync(
                GetRequiredUserId(),
                new CompleteCheckoutCommand
                {
                    OrderId = request.OrderId,
                    PaymentIntentId = request.PaymentIntentId
                },
                cancellationToken);

            return Ok(result);
        }
        catch (OrderNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (CheckoutValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetRequiredUserId()
    {
        var userId = User.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Unauthorized.");
        }

        return userId.Value;
    }

    public sealed class CheckoutQuoteRequest
    {
        [MaxLength(100)]
        public string? DiscountCode { get; set; }
    }

    public sealed class CreateCheckoutPaymentIntentRequest
    {
        [Required(ErrorMessage = "Enter the customer name.")]
        [MinLength(OrderValidation.CustomerNameMinLength, ErrorMessage = "Customer name must be at least 2 characters.")]
        [MaxLength(OrderValidation.CustomerNameMaxLength, ErrorMessage = "Customer name must be 120 characters or fewer.")]
        [RegularExpression(OrderValidation.PersonNamePattern, ErrorMessage = OrderValidation.CustomerNameMessage)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the customer email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(OrderValidation.CustomerEmailMaxLength, ErrorMessage = "Email address must be 255 characters or fewer.")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the customer phone number.")]
        [MaxLength(OrderValidation.CustomerPhoneMaxLength, ErrorMessage = "Phone number must be 40 characters or fewer.")]
        [RegularExpression(OrderValidation.PhonePattern, ErrorMessage = OrderValidation.CustomerPhoneMessage)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DiscountCode { get; set; }

        [Required(ErrorMessage = "Enter the shipping country.")]
        [MaxLength(OrderValidation.ShippingLocationMaxLength, ErrorMessage = "Shipping country must be 120 characters or fewer.")]
        [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
        public string ShippingCountry { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the shipping city.")]
        [MaxLength(OrderValidation.ShippingLocationMaxLength, ErrorMessage = "Shipping city must be 120 characters or fewer.")]
        [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
        public string ShippingCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the shipping address.")]
        [MaxLength(OrderValidation.ShippingAddressMaxLength, ErrorMessage = "Shipping address must be 255 characters or fewer.")]
        [RegularExpression(OrderValidation.AddressPattern, ErrorMessage = OrderValidation.ShippingAddressMessage)]
        public string ShippingAddressLine1 { get; set; } = string.Empty;

        [MaxLength(OrderValidation.ShippingAddressMaxLength, ErrorMessage = "Address line 2 must be 255 characters or fewer.")]
        [RegularExpression(OrderValidation.AddressPattern, ErrorMessage = OrderValidation.ShippingAddressMessage)]
        public string? ShippingAddressLine2 { get; set; }

        [Required(ErrorMessage = "Enter the shipping postal code.")]
        [MaxLength(OrderValidation.ShippingPostalCodeMaxLength, ErrorMessage = "Postal code must be 40 characters or fewer.")]
        [RegularExpression(OrderValidation.PostalCodePattern, ErrorMessage = OrderValidation.ShippingPostalCodeMessage)]
        public string ShippingPostalCode { get; set; } = string.Empty;

        [MaxLength(OrderValidation.ShippingLocationMaxLength, ErrorMessage = "Shipping region must be 120 characters or fewer.")]
        [RegularExpression(OrderValidation.LocationPattern, ErrorMessage = OrderValidation.ShippingLocationMessage)]
        public string? ShippingRegion { get; set; }

        [MaxLength(OrderValidation.NotesMaxLength, ErrorMessage = "Notes must be 1000 characters or fewer.")]
        public string? Notes { get; set; }
    }

    public sealed class CompleteCheckoutRequest
    {
        [Range(1, int.MaxValue)]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(255)]
        public string PaymentIntentId { get; set; } = string.Empty;
    }
}
