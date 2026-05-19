namespace GreenHerb.Application.Features.Cart.Dtos;

public sealed class AddCartItemCommand
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartItemCommand
{
    public int Quantity { get; set; }
}

public sealed class MergeCartCommand
{
    public List<MergeCartItemInput> Items { get; set; } = [];
}

public sealed class MergeCartItemInput
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class CartDto
{
    public List<CartItemDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public int ItemCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CartItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public CartProductDto Product { get; set; } = null!;
}

public sealed class CartProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public DateTime CreatedAt { get; set; }
}
