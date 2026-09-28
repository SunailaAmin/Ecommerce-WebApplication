namespace ECommerce.Infrastructure.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
