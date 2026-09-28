using ECommerce.Application.DTOs.Addresses;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class AddressServiceTests
{
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly AddressService _service;

    public AddressServiceTests()
    {
        _service = new AddressService(
            _addressRepository.Object,
            _unitOfWork.Object,
            NullLogger<AddressService>.Instance);
    }

    private static Address CreateAddress(int userId = 1, int id = 7) => new()
    {
        Id = id,
        UserId = userId,
        FullName = "Test Recipient",
        AddressLine = "1 Test Street",
        City = "Testville",
        State = "TestState",
        PostalCode = "12345",
        Country = "TestCountry",
        PhoneNumber = "5550100"
    };

    private static CreateAddressRequest CreateRequest(
        string fullName = "Test Recipient",
        string? state = "TestState",
        string? phone = "5550100") => new()
    {
        FullName = fullName,
        AddressLine = "1 Test Street",
        City = "Testville",
        State = state,
        PostalCode = "12345",
        Country = "TestCountry",
        PhoneNumber = phone
    };

    [Fact]
    public async Task GetAddressesAsync_ReturnsTheCustomersAddresses()
    {
        _addressRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address> { CreateAddress(), CreateAddress(id: 8) });

        var result = await _service.GetAddressesAsync(1, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Testville", result[0].City);
    }

    [Fact]
    public async Task CreateAddressAsync_StoresTrimmedValues()
    {
        Address? captured = null;
        _addressRepository
            .Setup(x => x.Add(It.IsAny<Address>()))
            .Callback<Address>(a => captured = a);

        var result = await _service.CreateAddressAsync(
            1,
            CreateRequest(fullName: "  Test Recipient  ", state: " TestState ", phone: " 5550100 "),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Test Recipient", captured!.FullName);
        Assert.Equal("TestState", captured.State);
        Assert.Equal("5550100", captured.PhoneNumber);
        Assert.Equal(1, captured.UserId);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Test Recipient", result.FullName);
    }

    [Fact]
    public async Task CreateAddressAsync_NormalisesBlankOptionalFieldsToNull()
    {
        Address? captured = null;
        _addressRepository
            .Setup(x => x.Add(It.IsAny<Address>()))
            .Callback<Address>(a => captured = a);

        await _service.CreateAddressAsync(1, CreateRequest(state: "   ", phone: null), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured!.State);
        Assert.Null(captured.PhoneNumber);
    }

    [Fact]
    public async Task CreateAddressAsync_AlwaysUsesTheTokenUser_NotTheRequest()
    {
        Address? captured = null;
        _addressRepository
            .Setup(x => x.Add(It.IsAny<Address>()))
            .Callback<Address>(a => captured = a);

        await _service.CreateAddressAsync(9, CreateRequest(), CancellationToken.None);

        Assert.Equal(9, captured!.UserId);
    }

    [Fact]
    public async Task UpdateAddressAsync_UpdatesOwnedAddress()
    {
        var address = CreateAddress();
        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var result = await _service.UpdateAddressAsync(
            1,
            7,
            new UpdateAddressRequest
            {
                FullName = "Updated Name",
                AddressLine = "2 New Street",
                City = "Newville",
                State = null,
                PostalCode = "99999",
                Country = "NewCountry",
                PhoneNumber = null
            },
            CancellationToken.None);

        Assert.Equal("Updated Name", address.FullName);
        Assert.Equal("Newville", address.City);
        Assert.Null(address.State);
        Assert.Null(address.PhoneNumber);
        Assert.Equal("Updated Name", result.FullName);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAddressAsync_ThrowsNotFound_WhenAddressBelongsToAnotherCustomer()
    {
        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(userId: 2));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAddressAsync(
            1,
            7,
            new UpdateAddressRequest
            {
                FullName = "Updated",
                AddressLine = "2 New Street",
                City = "Newville",
                PostalCode = "99999",
                Country = "NewCountry"
            },
            CancellationToken.None));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAddressAsync_ThrowsNotFound_WhenAddressMissing()
    {
        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAddressAsync(
            1,
            7,
            new UpdateAddressRequest
            {
                FullName = "Updated",
                AddressLine = "2 New Street",
                City = "Newville",
                PostalCode = "99999",
                Country = "NewCountry"
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAddressAsync_DeletesOwnedAddress()
    {
        var address = CreateAddress();

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        _addressRepository
            .Setup(x => x.IsUsedByAnyOrderAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.DeleteAddressAsync(1, 7, CancellationToken.None);

        _addressRepository.Verify(x => x.Delete(address), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAddressAsync_ThrowsValidation_WhenUsedByAnOrder()
    {
        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress());

        _addressRepository
            .Setup(x => x.IsUsedByAnyOrderAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteAddressAsync(1, 7, CancellationToken.None));

        _addressRepository.Verify(x => x.Delete(It.IsAny<Address>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAddressAsync_ThrowsNotFound_WhenAddressBelongsToAnotherCustomer()
    {
        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(userId: 2));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAddressAsync(1, 7, CancellationToken.None));

        _addressRepository.Verify(x => x.Delete(It.IsAny<Address>()), Times.Never);
    }
}
