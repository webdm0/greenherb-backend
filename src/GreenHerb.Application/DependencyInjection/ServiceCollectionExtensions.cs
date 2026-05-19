using GreenHerb.Application.Features.Auth.Interfaces;
using GreenHerb.Application.Features.Auth.Services;
using GreenHerb.Application.Features.Cart.Interfaces;
using GreenHerb.Application.Features.Cart.Services;
using GreenHerb.Application.Features.Checkout.Interfaces;
using GreenHerb.Application.Features.Checkout.Services;
using GreenHerb.Application.Features.Orders.Interfaces;
using GreenHerb.Application.Features.Orders.Services;
using GreenHerb.Application.Features.Products.Interfaces;
using GreenHerb.Application.Features.Products.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GreenHerb.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        return services;
    }
}
