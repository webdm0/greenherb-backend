using GreenHerb.Application.Features.Orders.Dtos;

namespace GreenHerb.Application.Features.Orders.Interfaces;

public interface IOrderService
{
    Task<List<OrderHistoryDto>> GetAsync(int userId, CancellationToken cancellationToken = default);
}
