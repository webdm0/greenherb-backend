using GreenHerb.Application.Abstractions.Persistence;
using GreenHerb.Application.Features.Orders.Dtos;
using GreenHerb.Application.Features.Orders.Interfaces;
using GreenHerb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GreenHerb.Application.Features.Orders.Services;

public sealed class OrderService(IAppDbContext dbContext) : IOrderService
{
    public async Task<List<OrderHistoryDto>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId && order.Status == OrderStatus.Paid)
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderHistoryDto
            {
                Id = order.Id,
                OrderReference = order.OrderReference,
                Status = order.Status.ToString(),
                Currency = order.Currency,
                SubtotalAmount = order.SubtotalAmount,
                DiscountAmount = order.DiscountAmount,
                DiscountCode = order.DiscountCode,
                TotalAmount = order.TotalAmount,
                ShippingAmount = order.ShippingAmount,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                ShippingCountry = order.ShippingCountry,
                ShippingCity = order.ShippingCity,
                ShippingAddressLine1 = order.ShippingAddressLine1,
                ShippingAddressLine2 = order.ShippingAddressLine2,
                ShippingPostalCode = order.ShippingPostalCode,
                ShippingRegion = order.ShippingRegion,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                Items = order.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderHistoryItemDto
                    {
                        Id = item.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        ProductSlug = item.ProductSlug,
                        ProductSku = item.ProductSku,
                        ProductImageUrl = item.ProductImageUrl,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
