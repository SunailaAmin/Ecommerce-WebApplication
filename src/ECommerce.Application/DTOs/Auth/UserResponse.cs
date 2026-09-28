namespace ECommerce.Application.DTOs.Auth;

public record UserResponse(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
