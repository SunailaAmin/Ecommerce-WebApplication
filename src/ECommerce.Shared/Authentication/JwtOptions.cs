namespace ECommerce.Shared.Authentication;

/// <summary>
/// Token settings shared by every service. The issuer, audience and signing key are read
/// from the same configuration in all four services, which is what lets a token issued by
/// one service be accepted by the others without a handshake.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
