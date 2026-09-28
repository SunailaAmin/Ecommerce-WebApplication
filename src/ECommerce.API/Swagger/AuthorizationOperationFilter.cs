using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ECommerce.API.Swagger;

/// <summary>
/// Marks only the operations that really require a token. Applying the requirement to the
/// whole document would put a padlock on public endpoints such as the login route, which
/// would make the documented sign-in flow look broken.
/// </summary>
public class AuthorizationOperationFilter : IOperationFilter
{
    private const string SecuritySchemeId = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!RequiresAuthorization(context))
        {
            return;
        }

        operation.Security ??= [];

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeId)] = []
        });
    }

    private static bool RequiresAuthorization(OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var controller = method.DeclaringType;

        // [AllowAnonymous] wins over [Authorize], so the padlock must follow it too.
        if (HasAllowAnonymous(method) || (controller is not null && HasAllowAnonymous(controller)))
        {
            return false;
        }

        return HasAuthorize(method) || (controller is not null && HasAuthorize(controller));
    }

    private static bool HasAuthorize(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

    private static bool HasAllowAnonymous(MemberInfo member) =>
        member.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
}
