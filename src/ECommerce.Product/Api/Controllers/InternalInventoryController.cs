using ECommerce.Products.Application.DTOs.Inventory;
using ECommerce.Products.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Products.Api.Controllers;

/// <summary>
/// Service-to-service inventory operations. The route lives under /internal, which the
/// InternalServiceKeyMiddleware gates on the X-Service-Key header, and the actions are
/// anonymous because the caller is a service, not a signed-in user. Nothing here is
/// reachable from the storefront.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("internal/inventory")]
public class InternalInventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InternalInventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId:int}/snapshot")]
    public async Task<ActionResult<InventorySnapshotResponse>> GetSnapshot(int productId, CancellationToken cancellationToken)
    {
        return await _inventoryService.GetSnapshotAsync(productId, cancellationToken);
    }

    [HttpPost("reserve")]
    public async Task<ActionResult<StockReservationResponse>> Reserve(StockMutationRequest request, CancellationToken cancellationToken)
    {
        return await _inventoryService.ReserveAsync(request, cancellationToken);
    }

    [HttpPost("release")]
    public async Task<ActionResult<StockReservationResponse>> Release(StockMutationRequest request, CancellationToken cancellationToken)
    {
        return await _inventoryService.ReleaseAsync(request, cancellationToken);
    }
}
