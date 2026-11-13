using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Inventory;
using ECommerce.Products.Application.Services;
using ECommerce.Shared.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(PagedResult<InventoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InventoryResponse>>> GetInventory(
        [FromQuery] InventoryQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _inventoryService.GetInventoryAsync(query, cancellationToken));
    }

    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryResponse>> GetInventoryByProduct(int productId, CancellationToken cancellationToken)
    {
        return Ok(await _inventoryService.GetInventoryByProductIdAsync(productId, cancellationToken));
    }

    [HttpPut("{productId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryResponse>> SetQuantity(
        int productId,
        [FromBody] UpdateInventoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _inventoryService.SetQuantityAsync(productId, request, cancellationToken));
    }

    [HttpPost("{productId:int}/adjust")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryResponse>> AdjustQuantity(
        int productId,
        [FromBody] AdjustInventoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _inventoryService.AdjustQuantityAsync(productId, request, cancellationToken));
    }
}
