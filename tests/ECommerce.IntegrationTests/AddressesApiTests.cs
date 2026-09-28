using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Addresses;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class AddressesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AddressesApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Addresses_RequireAuthentication()
    {
        var client = _factory.CreateClient();

        var get = await client.GetAsync("/api/addresses");
        var post = await client.PostAsJsonAsync("/api/addresses", TestData.NewAddress());
        var put = await client.PutAsJsonAsync("/api/addresses/1", TestData.NewAddress());
        var delete = await client.DeleteAsync("/api/addresses/1");

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task CreateAddress_ReturnsCreated()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/addresses", TestData.NewAddress());
        var created = await response.Content.ReadFromJsonAsync<AddressResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(created!.Id > 0);
        Assert.Equal("Test Recipient", created.FullName);
        Assert.Equal("12345", created.PostalCode);
    }

    [Fact]
    public async Task GetAddresses_ReturnsOnlyTheCustomersOwnAddresses()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        await TestData.CreateAddressAsync(customer, "MyTown");
        await TestData.CreateAddressAsync(other, "OtherTown");

        var response = await customer.GetAsync("/api/addresses");
        var addresses = await response.Content.ReadFromJsonAsync<List<AddressResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(addresses!);
        Assert.Equal("MyTown", addresses![0].City);
    }

    [Fact]
    public async Task GetAddresses_ReturnsEmptyList_ForNewCustomer()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");

        Assert.Empty(addresses!);
    }

    [Fact]
    public async Task CreateAddress_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            FullName = "",
            AddressLine = "",
            City = "",
            PostalCode = "",
            Country = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAddress_UpdatesOwnedAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await customer.PutAsJsonAsync($"/api/addresses/{address.Id}", new UpdateAddressRequest
        {
            FullName = "Updated Name",
            AddressLine = "99 New Road",
            City = "NewTown",
            PostalCode = "55555",
            Country = "NewCountry"
        });
        var updated = await response.Content.ReadFromJsonAsync<AddressResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated Name", updated!.FullName);
        Assert.Equal("NewTown", updated.City);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsNotFound_ForAnotherCustomersAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await other.PutAsJsonAsync($"/api/addresses/{address.Id}", new UpdateAddressRequest
        {
            FullName = "Hijacked",
            AddressLine = "99 New Road",
            City = "NewTown",
            PostalCode = "55555",
            Country = "NewCountry"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsNotFound_ForMissingAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PutAsJsonAsync("/api/addresses/999999", new UpdateAddressRequest
        {
            FullName = "Updated",
            AddressLine = "99 New Road",
            City = "NewTown",
            PostalCode = "55555",
            Country = "NewCountry"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAddress_RemovesOwnedAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await customer.DeleteAsync($"/api/addresses/{address.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");
        Assert.Empty(addresses!);
    }

    [Fact]
    public async Task DeleteAddress_ReturnsNotFound_ForAnotherCustomersAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await other.DeleteAsync($"/api/addresses/{address.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");
        Assert.Single(addresses!);
    }

    [Fact]
    public async Task DeleteAddress_ReturnsNotFound_ForMissingAddress()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.DeleteAsync("/api/addresses/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
