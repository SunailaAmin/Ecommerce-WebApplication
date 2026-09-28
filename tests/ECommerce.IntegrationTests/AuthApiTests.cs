using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Domain;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ECommerce.IntegrationTests;

public class AuthApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ReturnsTokenAndCustomerRole()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestData.NewUser(email));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(email.ToLowerInvariant(), auth.User.Email);
        Assert.Equal(RoleNames.Customer, auth.User.Role);
        Assert.True(auth.User.IsActive);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyRegistered()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/register", TestData.NewUser(email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "User", "Passw0rd!")]
    [InlineData("Test", "", "Passw0rd!")]
    [InlineData("Test", "User", "")]
    [InlineData("Test", "User", "short1")]
    [InlineData("Test", "User", "alllettersnodigits")]
    [InlineData("Test", "User", "12345678")]
    public async Task Register_ReturnsBadRequest_ForInvalidInput(string firstName, string lastName, string password)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = TestData.UniqueEmail(),
            Password = password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_ForMalformedEmail()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestData.NewUser("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForRegisteredUser()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = TestData.Password
        });
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email.ToUpperInvariant(),
            Password = TestData.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForWrongPassword()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPass1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForUnknownEmail()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = TestData.UniqueEmail(),
            Password = TestData.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DoesNotRevealWhetherAccountExists()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPass1"
        });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = TestData.UniqueEmail(),
            Password = "WrongPass1"
        });

        var first = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var second = await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(first!.Status, second!.Status);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Detail, second.Detail);
    }

    [Fact]
    public async Task Register_StoresPasswordHash_NotPlainText()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(x => x.Email == email);

        Assert.NotNull(user);
        Assert.NotEqual(TestData.Password, user!.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public async Task Register_StoredHashVerifies_WithBuiltInHasher()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(x => x.Email == email);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var result = hasher.VerifyHashedPassword(user!, user!.PasswordHash, TestData.Password);

        Assert.NotEqual(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public async Task Register_StoredHashRejectsWrongPassword()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(x => x.Email == email);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var result = hasher.VerifyHashedPassword(user!, user!.PasswordHash, "WrongPass1");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public async Task Login_SucceedsAgainstStoredHash_ProvingHashFormatIsCompatible()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        await TestData.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = TestData.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsRegisteredAccount()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail();

        var auth = await TestData.RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var user = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.NotNull(user);
        Assert.Equal(auth.User.Id, user!.Id);
        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.Equal(RoleNames.Customer, user.Role);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_ForGarbageToken()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_ForTokenSignedWithDifferentKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTokenWithWrongKey());

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Seeder_CreatesAdminAndCustomerRoles()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var roles = await context.Roles.ToListAsync();

        Assert.Contains(roles, r => r.Name == RoleNames.Admin);
        Assert.Contains(roles, r => r.Name == RoleNames.Customer);
    }

    private static string CreateTokenWithWrongKey()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, RoleNames.Admin)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)));

        var token = new JwtSecurityToken(
            issuer: "ECommerce.API",
            audience: "ECommerce.API",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
