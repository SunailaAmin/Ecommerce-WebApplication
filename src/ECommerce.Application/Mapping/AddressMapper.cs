using ECommerce.Application.DTOs.Addresses;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Mapping;

/// <summary>
/// Shared address mapping. Used by the address book and by order responses so the
/// same address is never shaped two different ways.
/// </summary>
public static class AddressMapper
{
    public static AddressResponse ToResponse(this Address address) => new(
        address.Id,
        address.FullName,
        address.AddressLine,
        address.City,
        address.State,
        address.PostalCode,
        address.Country,
        address.PhoneNumber);
}
