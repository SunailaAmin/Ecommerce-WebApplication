using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerce.Products.Infrastructure.Middleware;

/// <summary>
/// Guards the `/internal` surface, which exists only for service-to-service traffic and must
/// never be reachable from a browser. The key is a shared static secret held in
/// configuration, so a missing or empty setting fails closed rather than leaving the
/// endpoint open. Comparison is fixed-time so the check itself cannot be timed.
/// </summary>
public class InternalServiceKeyMiddleware
{
    public const string HeaderName = "X-Service-Key";
    public const string ConfigurationKey = "Services:InternalServiceKey";

    private const string PathPrefix = "/internal";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalServiceKeyMiddleware> _logger;

    public InternalServiceKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalServiceKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(PathPrefix))
        {
            await _next(context);
            return;
        }

        var expectedKey = _configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            _logger.LogError(
                "{ConfigurationKey} is not configured, so internal request to {Path} was rejected.",
                ConfigurationKey,
                context.Request.Path);

            await RejectAsync(context, "Internal service access is not configured.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            await RejectAsync(context, $"Internal requests must carry the {HeaderName} header.");
            return;
        }

        if (!KeyEquals(expectedKey, provided.ToString()))
        {
            _logger.LogWarning("Rejected internal request to {Path} with an invalid service key.", context.Request.Path);
            await RejectAsync(context, "Invalid service key.");
            return;
        }

        await _next(context);
    }

    private static bool KeyEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static async Task RejectAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status403Forbidden,
            title = "Forbidden",
            detail,
            instance = context.Request.Path.Value
        });
    }
}
