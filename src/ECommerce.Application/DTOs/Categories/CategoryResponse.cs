namespace ECommerce.Application.DTOs.Categories;

public record CategoryResponse(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
