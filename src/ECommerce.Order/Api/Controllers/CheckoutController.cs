using ECommerce.Shared.Security;
using ECommerce.Orders.Application.DTOs.Payments;
using ECommerce.Orders.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Orders.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/checkout")]
[Produces("application/json")]
public class CheckoutController : ControllerBase
{
    private readonly CheckoutService _checkoutService;

    public CheckoutController(CheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _checkoutService.CheckoutAsync(User.CurrentUserId(), request, cancellationToken);

        return Created($"/api/orders/{response.Order.Id}", response);
    }
}
