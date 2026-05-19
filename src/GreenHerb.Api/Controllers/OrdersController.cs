using GreenHerb.Api.Extensions;
using GreenHerb.Application.Features.Orders.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GreenHerb.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetAsync(GetRequiredUserId(), cancellationToken);
        return Ok(orders);
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
}
