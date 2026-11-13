using System.Globalization;
using System.Security.Claims;

namespace ECommerce.Shared.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the caller's user id from the access token. Endpoints that use this must be
    /// marked [Authorize], which is what guarantees the claim is present.
    /// </summary>
    public static int CurrentUserId(this ClaimsPrincipal user)
    {
        return int.Parse(
            user.FindFirstValue(ClaimTypes.NameIdentifier)!,
            CultureInfo.InvariantCulture);
    }
}
