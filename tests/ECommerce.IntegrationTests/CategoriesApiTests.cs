using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Categories;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class CategoriesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CategoriesApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCategories_ReturnsList()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");

        Assert.NotNull(categories);
        Assert.Contains(categories!, c => c.Id == category.Id);
    }

    [Fact]
    public async Task GetCategories_ExcludesInactive_UnlessRequested()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        await client.DeleteAsync($"/api/categories/{category.Id}");

        var active = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");
        var all = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories?includeInactive=true");

        Assert.DoesNotContain(active!, c => c.Id == category.Id);
        Assert.Contains(all!, c => c.Id == category.Id);
    }

    [Fact]
    public async Task GetCategory_ReturnsCategory()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var result = await client.GetFromJsonAsync<CategoryResponse>($"/api/categories/{category.Id}");

        Assert.Equal(category.Id, result!.Id);
        Assert.Equal(category.Name, result.Name);
    }

    [Fact]
    public async Task GetCategory_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_ReturnsCreated()
    {
        var client = await _factory.CreateAdminClientAsync();
        var name = TestData.UniqueName("Fresh");

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = name,
            Description = "A new category"
        });

        var created = await response.Content.ReadFromJsonAsync<CategoryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(name, created!.Name);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task CreateCategory_ReturnsConflict_WhenNameIsDuplicate()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = category.Name
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_ReturnsBadRequest_WhenNameMissing()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_AppliesChanges()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var response = await client.PutAsJsonAsync($"/api/categories/{category.Id}", new UpdateCategoryRequest
        {
            Name = TestData.UniqueName("Renamed"),
            Description = "Updated",
            IsActive = true
        });

        var updated = await response.Content.ReadFromJsonAsync<CategoryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated", updated!.Description);
    }

    [Fact]
    public async Task UpdateCategory_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync("/api/categories/999999", new UpdateCategoryRequest
        {
            Name = TestData.UniqueName("Ghost")
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsNoContent()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var response = await client.DeleteAsync($"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.DeleteAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
