using ECommerce.API.Extensions;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly PaymentService _paymentService;

    public OrdersController(OrderService orderService, PaymentService paymentService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _orderService.CreateOrderAsync(User.CurrentUserId(), request, cancellationToken);

        return Created($"/api/orders/{response.Id}", response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _orderService.GetOrdersAsync(User.CurrentUserId(), page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.GetOrderByIdAsync(User.CurrentUserId(), id, isAdmin: false, cancellationToken));
    }

    [HttpGet("{id:int}/payment")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentForOrder(int id, CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.GetForOrderAsync(User.CurrentUserId(), id, isAdmin: false, cancellationToken));
    }
}
