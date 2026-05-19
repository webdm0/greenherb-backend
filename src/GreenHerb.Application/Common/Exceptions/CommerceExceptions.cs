namespace GreenHerb.Application.Common.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException()
        : base("Product not found.")
    {
    }
}

public sealed class CartItemNotFoundException : Exception
{
    public CartItemNotFoundException()
        : base("Cart item not found.")
    {
    }
}

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException()
        : base("Order not found.")
    {
    }
}

public sealed class CheckoutValidationException : Exception
{
    public CheckoutValidationException(string message)
        : base(message)
    {
    }
}
