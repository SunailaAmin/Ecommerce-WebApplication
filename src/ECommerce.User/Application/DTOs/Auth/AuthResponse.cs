namespace ECommerce.Users.Application.DTOs.Auth;

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    UserResponse User);
