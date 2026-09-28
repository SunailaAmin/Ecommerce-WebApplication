using System.Net.Http.Headers;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Domain;
using ECommerce.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestPassword = "Passw0rd!";

    private readonly string _databaseName = $"ECommerceTest_{Guid.NewGuid():N}";

    private Task<HttpClient>? _adminClient;
    private Task<HttpClient>? _customerClient;

    /// <summary>
    /// Each factory owns a database, so test classes never share state. Held in a field
    /// because a fresh name per access would point the connection string at a new database
    /// on every read.
    /// </summary>
    protected virtual string DatabaseName => _databaseName;

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", new string('k', 64));

        ConfigureTestHost(builder);
    }

    /// <summary>
    /// Hook for a derived factory that needs extra configuration.
    /// </summary>
    protected virtual void ConfigureTestHost(IWebHostBuilder builder)
    {
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        await context.Database.EnsureDeletedAsync();
    }

    public Task<HttpClient> CreateAdminClientAsync() =>
        _adminClient ??= CreateAuthenticatedClientAsync(RoleNames.Admin);

    public Task<HttpClient> CreateCustomerClientAsync() =>
        _customerClient ??= CreateAuthenticatedClientAsync(RoleNames.Customer);

    /// <summary>
    /// A brand new customer every call, for tests that need a cart and orders of their
    /// own instead of sharing the cached customer's data.
    /// </summary>
    public Task<HttpClient> CreateFreshCustomerClientAsync() =>
        CreateAuthenticatedClientAsync(RoleNames.Customer);

    /// <summary>
    /// Same as <see cref="CreateFreshCustomerClientAsync"/> but also hands back the token
    /// and user id, which is what a SignalR connection needs.
    /// </summary>
    public Task<AuthenticatedUser> CreateFreshCustomerAsync() =>
        CreateUserAsync(RoleNames.Customer);

    public Task<AuthenticatedUser> CreateAdminAsync() =>
        CreateUserAsync(RoleNames.Admin);

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role) =>
        (await CreateUserAsync(role)).Client;

    private async Task<AuthenticatedUser> CreateUserAsync(string role)
    {
        var client = CreateClient();
        var email = TestData.UniqueEmail();

        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Password = TestPassword
        });

        registration.EnsureSuccessStatusCode();

        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            var roleId = await context.Roles
                .Where(x => x.Name == role)
                .Select(x => x.Id)
                .SingleAsync();

            var user = await context.Users.SingleAsync(x => x.Email == email);
            user.RoleId = roleId;
            await context.SaveChangesAsync();
        }

        var auth = await TestData.LoginAsync(client, email, TestPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        return new AuthenticatedUser(client, auth.Token, auth.User.Id);
    }
}

/// <summary>
/// A signed-in test user along with the credentials a long-lived connection would use.
/// </summary>
public record AuthenticatedUser(HttpClient Client, string Token, int UserId);
