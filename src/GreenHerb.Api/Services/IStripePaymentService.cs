using GreenHerb.Application.Features.Checkout.Dtos;
using GreenHerb.Application.Features.Checkout.Interfaces;

namespace GreenHerb.Api.Services;

public interface IStripePromotionService : ICheckoutPromotionService;

public interface IStripePaymentService : ICheckoutPaymentGateway;
