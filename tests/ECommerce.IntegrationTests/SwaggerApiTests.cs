using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Application.DTOs.Auth;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Guards the Swagger/OpenAPI document itself. A dropped endpoint, a lost security
/// requirement or a response that no longer advertises its schema is a regression worth
/// failing the build for. Prose is not guarded on purpose: the goal asks for XML comments
/// only where they add value, and the prose lives in the README instead.
/// </summary>
public class SwaggerApiTests : IClassFixture<ApiFactory>
{
    private const string Anonymous = "anonymous";

    /// <summary>
    /// Every endpoint the API is expected to publish. A controller that is accidentally
    /// dropped or mis-routed shows up here as a missing path.
    /// </summary>
    private static readonly (string Method, string Path, bool IsProtected)[] ExpectedOperations =
    [
        ("GET", "/api/products", false),
        ("GET", "/api/products/{id}", false),
        ("POST", "/api/products", true),
        ("PUT", "/api/products/{id}", true),
        ("DELETE", "/api/products/{id}", true),
        ("GET", "/api/categories", false),
        ("GET", "/api/categories/{id}", false),
        ("POST", "/api/categories", true),
        ("PUT", "/api/categories/{id}", true),
        ("DELETE", "/api/categories/{id}", true),
        ("GET", "/api/inventory", true),
        ("GET", "/api/inventory/{productId}", false),
        ("PUT", "/api/inventory/{productId}", true),
        ("POST", "/api/inventory/{productId}/adjust", true),
        ("POST", "/api/auth/register", false),
        ("POST", "/api/auth/login", false),
        ("GET", "/api/auth/me", true),
        ("GET", "/api/cart", true),
        ("DELETE", "/api/cart", true),
        ("POST", "/api/cart/items", true),
        ("PUT", "/api/cart/items/{itemId}", true),
        ("DELETE", "/api/cart/items/{itemId}", true),
        ("GET", "/api/addresses", true),
        ("POST", "/api/addresses", true),
        ("PUT", "/api/addresses/{id}", true),
        ("DELETE", "/api/addresses/{id}", true),
        ("GET", "/api/orders", true),
        ("POST", "/api/orders", true),
        ("GET", "/api/orders/{id}", true),
        ("GET", "/api/orders/{id}/payment", true),
        ("GET", "/api/admin/orders", true),
        ("GET", "/api/admin/orders/{id}", true),
        ("PUT", "/api/admin/orders/{id}/status", true),
        ("POST", "/api/checkout", true),
        ("POST", "/api/payments", true),
        ("GET", "/api/payments/{id}", true)
    ];

    private readonly ApiFactory _factory;

    public SwaggerApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Yields every operation in the document as (method, path, operation). In OpenAPI 3 the
    /// HTTP method is a property of the path item rather than a field on the operation.
    /// </summary>
    private static IEnumerable<(string Method, string Path, JsonElement Operation)> Operations(JsonElement paths)
    {
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return (operation.Name.ToUpperInvariant(), path.Name, operation.Value);
                }
            }
        }
    }

    [Fact]
    public async Task SwaggerJson_IsServedInDevelopment()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryEndpointIsPublished()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        var missing = ExpectedOperations
            .Where(op => !HasOperation(paths, op.Method, op.Path))
            .Select(op => $"{op.Method} {op.Path}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task NoEndpointIsPublishedBeyondTheExpectedSurface()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        var unexpected = Operations(paths)
            .Select(op => $"{op.Method} {op.Path}")
            .Where(label => !ExpectedOperations.Any(expected => $"{expected.Method} {expected.Path}" == label))
            .ToList();

        Assert.Empty(unexpected);
    }

    [Fact]
    public async Task EveryOperationIsDocumented()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        foreach (var (method, path, operation) in Operations(paths))
        {
            // Prose summaries are deliberately absent: the goal asks for XML comments only
            // where they add value, and the descriptions lived in the README. What has to
            // stay is the machine-readable half - the response status codes and their
            // schemas, which is what a caller actually reads.
            Assert.True(operation.GetProperty("responses").EnumerateObject().Any(),
                $"{method} {path} documents no response status codes.");
        }
    }

    [Fact]
    public async Task EveryDocumentedSuccessResponseHasASchema()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        foreach (var (method, path, operation) in Operations(paths))
        {
            foreach (var response in operation.GetProperty("responses").EnumerateObject())
            {
                var hasContent = response.Value.TryGetProperty("content", out var content)
                    && content.EnumerateObject().Any();

                // 204 is the only success code that carries no body.
                if (response.Name is "200" or "201")
                {
                    Assert.True(hasContent, $"{method} {path} documents {response.Name} with no response schema.");
                }
            }
        }
    }

    [Fact]
    public async Task ProtectedOperationsDeclareTheBearerRequirement()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        var wrong = new List<string>();

        foreach (var (method, path, isProtected) in ExpectedOperations)
        {
            var operation = paths.GetProperty(path).GetProperty(method.ToLowerInvariant());

            var declaresSecurity = operation.TryGetProperty("security", out var security)
                && security.GetArrayLength() > 0;

            if (declaresSecurity != isProtected)
            {
                wrong.Add($"{method} {path} expected protected={isProtected} but documented={declaresSecurity}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public async Task TheBearerSecuritySchemeIsDeclared()
    {
        using var document = await GetDocumentAsync();

        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");

        // Declaring the scheme is what makes Swagger UI show its Authorize button.
        Assert.True(schemes.TryGetProperty("Bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task TheDocumentHasNoBlanketSecurityRequirement()
    {
        using var document = await GetDocumentAsync();

        // A document-wide requirement would put a padlock on the public sign-in routes.
        Assert.False(document.RootElement.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task TheDocumentIsIdentified()
    {
        using var document = await GetDocumentAsync();
        var info = document.RootElement.GetProperty("info");

        Assert.Equal("ECommerce API", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task NoSchemaExposesASecret()
    {
        using var document = await GetDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var forbidden = new[] { "passwordhash", "rowversion", "jwtkey", "secret", "connectionstring" };
        var offenders = new List<string>();

        foreach (var schema in schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            foreach (var property in properties.EnumerateObject())
            {
                if (forbidden.Contains(property.Name.Replace("_", string.Empty).ToLowerInvariant()))
                {
                    offenders.Add($"{schema.Name}.{property.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task ProtectedEndpointsReturnUnauthorized_WithoutAToken()
    {
        var client = _factory.CreateClient();

        foreach (var (method, path, _) in ExpectedOperations.Where(x => x.IsProtected))
        {
            var request = new HttpRequestMessage(new HttpMethod(method), path
                .Replace("{id}", "1")
                .Replace("{itemId}", "1")
                .Replace("{productId}", "1"));

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task AProtectedEndpointAcceptsTheTokenThatSwaggerWouldAuthorizeWith()
    {
        var client = await _factory.CreateCustomerClientAsync();

        // The same bearer token the Authorize dialog collects.
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
    }

    [Fact]
    public async Task APublicEndpointWorks_WithoutAToken()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static bool HasOperation(JsonElement paths, string method, string path) =>
        paths.TryGetProperty(path, out var item)
        && item.TryGetProperty(method.ToLowerInvariant(), out _);
}
