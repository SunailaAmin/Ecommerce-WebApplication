using ECommerce.Shared.Security;
using ECommerce.Orders.Application.DTOs.Cart;
using ECommerce.Orders.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Orders.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
[Produces("application/json")]
public class CartController : ControllerBase
{
    private readonly CartService _cartService;

    public CartController(CartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartResponse>> GetCart(CancellationToken cancellationToken)
    {
        return Ok(await _cartService.GetCartAsync(User.CurrentUserId(), cancellationToken));
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _cartService.AddItemAsync(User.CurrentUserId(), request, cancellationToken));
    }

    [HttpPut("items/{itemId:int}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        int itemId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _cartService.UpdateItemAsync(User.CurrentUserId(), itemId, request, cancellationToken));
    }

    [HttpDelete("items/{itemId:int}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> RemoveItem(int itemId, CancellationToken cancellationToken)
    {
        return Ok(await _cartService.RemoveItemAsync(User.CurrentUserId(), itemId, cancellationToken));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        await _cartService.ClearCartAsync(User.CurrentUserId(), cancellationToken);

        return NoContent();
    }
}
