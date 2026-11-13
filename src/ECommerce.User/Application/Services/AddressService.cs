using ECommerce.Users.Application.DTOs.Addresses;
using ECommerce.Shared.Exceptions;
using ECommerce.Users.Application.Interfaces;
using ECommerce.Users.Application.Mapping;
using ECommerce.Users.Domain.Entities;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;
using ECommerce.Shared.DTOs;

namespace ECommerce.Users.Application.Services;

public class AddressService
{
    private readonly IAddressRepository _addressRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddressService> _logger;

    public AddressService(
        IAddressRepository addressRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddressService> logger)
    {
        _addressRepository = addressRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AddressResponse>> GetAddressesAsync(int userId, CancellationToken cancellationToken)
    {
        var addresses = await _addressRepository.GetByUserIdAsync(userId, cancellationToken);

        return addresses.Select(x => x.ToResponse()).ToList();
    }

    public async Task<AddressResponse> CreateAddressAsync(int userId, CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = new Address
        {
            UserId = userId,
            FullName = request.FullName.Trim(),
            AddressLine = request.AddressLine.Trim(),
            City = request.City.Trim(),
            State = Normalize(request.State),
            PostalCode = request.PostalCode.Trim(),
            Country = request.Country.Trim(),
            PhoneNumber = Normalize(request.PhoneNumber)
        };

        _addressRepository.Add(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Address {AddressId} created for user {UserId}", address.Id, userId);

        return address.ToResponse();
    }

    public async Task<AddressResponse> UpdateAddressAsync(
        int userId,
        int id,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var address = await GetOwnedAddressOrThrowAsync(userId, id, cancellationToken);

        address.FullName = request.FullName.Trim();
        address.AddressLine = request.AddressLine.Trim();
        address.City = request.City.Trim();
        address.State = Normalize(request.State);
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
        address.PhoneNumber = Normalize(request.PhoneNumber);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Address {AddressId} updated for user {UserId}", id, userId);

        return address.ToResponse();
    }

    public async Task DeleteAddressAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var address = await GetOwnedAddressOrThrowAsync(userId, id, cancellationToken);

        if (await _addressRepository.IsUsedByAnyOrderAsync(id, cancellationToken))
        {
            throw new ValidationException(
                "This address is used by an order and cannot be deleted.");
        }

        _addressRepository.Delete(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Address {AddressId} deleted for user {UserId}", id, userId);
    }

    /// <summary>
    /// Ownership is part of the lookup, so a customer cannot read, change or delete
    /// another customer's address and cannot tell that it exists.
    /// </summary>
    private async Task<Address> GetOwnedAddressOrThrowAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var address = await _addressRepository.GetByIdAsync(id, cancellationToken);

        if (address is null || address.UserId != userId)
        {
            throw new NotFoundException($"Address with id {id} was not found.");
        }

        return address;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
