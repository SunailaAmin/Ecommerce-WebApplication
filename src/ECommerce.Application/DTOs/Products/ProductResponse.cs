namespace ECommerce.Application.DTOs.Products;

public record ProductResponse(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    string Sku,
    string? ImageUrl,
    int CategoryId,
    string CategoryName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
