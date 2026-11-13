using ECommerce.Users.Domain.Entities;

namespace ECommerce.Users.Application.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateToken(User user);
}
