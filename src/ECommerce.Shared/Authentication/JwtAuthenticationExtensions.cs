using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Shared.Authentication;

/// <summary>
/// Identical token validation in every service, taken straight from the monolith: same
/// issuer, audience, signing key, zero clock skew and the same claim type mapping. A service
/// only differs in whether it also reads the token from the query string for its hub.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerOptions>? configure = null)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < JwtOptions.MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:Key' is not configured or is shorter than {JwtOptions.MinimumKeyLength} characters. " +
                $"Add it for local development with: dotnet user-secrets set \"{JwtOptions.SectionName}:Key\" \"<at least {JwtOptions.MinimumKeyLength} random characters>\"");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:Issuer' and '{JwtOptions.SectionName}:Audience' must both be configured.");
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                configure?.Invoke(options);
            });

        services.AddAuthorization();

        return services;
    }
}
