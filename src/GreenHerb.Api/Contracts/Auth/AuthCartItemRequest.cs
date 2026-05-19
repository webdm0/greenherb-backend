using System.ComponentModel.DataAnnotations;

namespace GreenHerb.Api.Contracts.Auth;

public sealed class AuthCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
