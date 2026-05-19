using GreenHerb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GreenHerb.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<ExternalIdentity> ExternalIdentities { get; }
    DbSet<RefreshSession> RefreshSessions { get; }
    DbSet<Product> Products { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
