using ECommerce.Shared.Security;
using ECommerce.Users.Application.DTOs.Addresses;
using ECommerce.Users.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Shared.DTOs;

namespace ECommerce.Users.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/addresses")]
[Produces("application/json")]
public class AddressesController : ControllerBase
{
    private readonly AddressService _addressService;

    public AddressesController(AddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> GetAddresses(CancellationToken cancellationToken)
    {
        return Ok(await _addressService.GetAddressesAsync(User.CurrentUserId(), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AddressResponse>> CreateAddress(
        [FromBody] CreateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _addressService.CreateAddressAsync(User.CurrentUserId(), request, cancellationToken);

        return Created("/api/addresses", response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressResponse>> UpdateAddress(
        int id,
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _addressService.UpdateAddressAsync(User.CurrentUserId(), id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken)
    {
        await _addressService.DeleteAddressAsync(User.CurrentUserId(), id, cancellationToken);

        return NoContent();
    }
}
