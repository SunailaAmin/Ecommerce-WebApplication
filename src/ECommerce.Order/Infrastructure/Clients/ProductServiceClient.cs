using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Orders.Application.DTOs.Products;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Options;
using ECommerce.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Orders.Infrastructure.Clients;

/// <summary>
/// Synchronous Order-to-Product calls over IHttpClientFactory. Base address, service key and
/// timeout are configuration, and every failure mode is mapped: transport and timeout become
/// a 502-shaped dependency failure, while 400/404/409 keep the domain meaning Product
/// returned so a stock conflict still reads as a conflict. Nothing from Product is echoed
/// back to the caller except its own problem detail text.
/// </summary>
public class ProductServiceClient : IProductServiceClient
{
    public const string ServiceKeyHeader = "X-Service-Key";

    private readonly HttpClient _httpClient;
    private readonly ProductServiceOptions _options;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(
        HttpClient httpClient,
        IOptions<ProductServiceOptions> options,
        ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InventorySnapshot> GetInventorySnapshotAsync(int productId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"internal/inventory/{productId}/snapshot",
            body: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadAsync<InventorySnapshot>(response, cancellationToken);
    }

    public async Task<StockReservationResult> ReserveStockAsync(StockMutationRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "internal/inventory/reserve",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadAsync<StockReservationResult>(response, cancellationToken);
    }

    public async Task<StockReservationResult> ReleaseStockAsync(StockMutationRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "internal/inventory/release",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadAsync<StockReservationResult>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(method, relativeUrl);
            message.Headers.Add(ServiceKeyHeader, _options.ServiceKey);

            if (body is not null)
            {
                message.Content = JsonContent.Create(body);
            }

            return await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Product Service timed out on {Method} {Url}", method, relativeUrl);

            throw new DependencyUnavailableException(
                "Product Service did not respond in time.",
                new TimeoutException());
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Product Service is unreachable at {BaseUrl} for {Method} {Url}",
                _httpClient.BaseAddress, method, relativeUrl);

            throw new DependencyUnavailableException("Product Service is unreachable.", exception);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new NotFoundException(detail ?? "The requested product was not found.");
            case HttpStatusCode.BadRequest:
                throw new ValidationException(detail ?? "Product Service rejected the request.");
            case HttpStatusCode.Conflict:
                throw new ConflictException(detail ?? "The requested stock is not available.");
            default:
                // 403, 408, 429, 5xx: a problem on the Product side or in the configuration
                // between the two, not something the caller can fix by changing the request.
                throw new DependencyUnavailableException(
                    $"Product Service returned {(int)response.StatusCode}.");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new DependencyUnavailableException("Product Service returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new DependencyUnavailableException("Product Service returned an unreadable response.", exception);
        }
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetail>(cancellationToken);

            return problem.Detail ?? problem.Title;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ProblemDetail(string? Title, string? Detail);
}
