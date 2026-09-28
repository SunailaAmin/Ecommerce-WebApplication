namespace ECommerce.Application.DTOs.Addresses;

public sealed record AddressResponse(
    int Id,
    string FullName,
    string AddressLine,
    string City,
    string? State,
    string PostalCode,
    string Country,
    string? PhoneNumber);
