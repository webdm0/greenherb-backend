namespace GreenHerb.Application.Features.Auth.Dtos;

public sealed class AuthCartItemInput
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
}
