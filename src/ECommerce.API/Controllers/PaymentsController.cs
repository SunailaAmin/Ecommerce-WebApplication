using ECommerce.API.Extensions;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Services;
using ECommerce.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> PayOrder(
        [FromBody] PaymentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _paymentService.PayExistingOrderAsync(
            User.CurrentUserId(),
            request,
            isAdmin: false,
            cancellationToken);

        return Created($"/api/payments/{response.Id}", response);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentById(int id, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(RoleNames.Admin);

        return Ok(await _paymentService.GetByIdAsync(User.CurrentUserId(), id, isAdmin, cancellationToken));
    }
}
