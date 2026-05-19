using System.ComponentModel.DataAnnotations;
using GreenHerb.Api.Extensions;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Cart.Dtos;
using GreenHerb.Application.Features.Cart.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GreenHerb.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetAsync(GetRequiredUserId(), cancellationToken);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await cartService.AddItemAsync(
                GetRequiredUserId(),
                new AddCartItemCommand
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity
                },
                cancellationToken);

            return Ok(cart);
        }
        catch (ProductNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut("items/{productId:int}")]
    public async Task<IActionResult> UpdateItem(int productId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await cartService.UpdateItemAsync(
                GetRequiredUserId(),
                productId,
                new UpdateCartItemCommand
                {
                    Quantity = request.Quantity
                },
                cancellationToken);

            return Ok(cart);
        }
        catch (CartItemNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId, CancellationToken cancellationToken)
    {
        var cart = await cartService.RemoveItemAsync(GetRequiredUserId(), productId, cancellationToken);
        return Ok(cart);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var cart = await cartService.ClearAsync(GetRequiredUserId(), cancellationToken);
        return Ok(cart);
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeCartRequest request, CancellationToken cancellationToken)
    {
        var cart = await cartService.MergeAsync(
            GetRequiredUserId(),
            new MergeCartCommand
            {
                Items = request.Items
                    .Select(item => new MergeCartItemInput
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    })
                    .ToList()
            },
            cancellationToken);

        return Ok(cart);
    }

    private int GetRequiredUserId()
    {
        var userId = User.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Unauthorized.");
        }

        return userId.Value;
    }

    public sealed class AddCartItemRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }

    public sealed class UpdateCartItemRequest
    {
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public sealed class MergeCartRequest
    {
        public List<MergeCartItemRequest> Items { get; set; } = [];
    }

    public sealed class MergeCartItemRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}
