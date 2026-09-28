using ECommerce.API.Extensions;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Services;
using ECommerce.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/orders")]
[Produces("application/json")]
public class AdminOrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public AdminOrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> GetOrders(
        [FromQuery] OrderQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _orderService.GetAllOrdersAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.GetOrderByIdAsync(User.CurrentUserId(), id, isAdmin: true, cancellationToken));
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(
        int id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _orderService.UpdateStatusAsync(id, request, cancellationToken));
    }
}
