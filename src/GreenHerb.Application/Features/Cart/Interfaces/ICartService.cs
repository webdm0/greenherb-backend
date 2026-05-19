using GreenHerb.Application.Features.Cart.Dtos;

namespace GreenHerb.Application.Features.Cart.Interfaces;

public interface ICartService
{
    Task<CartDto> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(int userId, AddCartItemCommand command, CancellationToken cancellationToken = default);
    Task<CartDto> UpdateItemAsync(int userId, int productId, UpdateCartItemCommand command, CancellationToken cancellationToken = default);
    Task<CartDto> RemoveItemAsync(int userId, int productId, CancellationToken cancellationToken = default);
    Task<CartDto> ClearAsync(int userId, CancellationToken cancellationToken = default);
    Task<CartDto> MergeAsync(int userId, MergeCartCommand command, CancellationToken cancellationToken = default);
}
