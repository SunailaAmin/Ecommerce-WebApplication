using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateToken(User user);
}
