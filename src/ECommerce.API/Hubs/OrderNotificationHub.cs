using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECommerce.API.Hubs;

/// <summary>
/// Real-time order notifications for the signed-in customer.
/// </summary>
/// <remarks>
/// A connection joins a group named after its own user id, so a notification is only ever
/// delivered to the customer it belongs to. The client sends the access token when it
/// connects; unauthenticated connections are refused.
/// </remarks>
[Authorize]
public class OrderNotificationHub : Hub
{
    public const string Route = "/hubs/order-notifications";

    public const string ClientMethod = "OrderNotification";

    /// <summary>
    /// Group name for one customer's connections. A customer may have several tabs open,
    /// and every one of them is in the same group.
    /// </summary>
    public static string GroupFor(int userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));

        await base.OnConnectedAsync();
    }

    private int CurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(value, out var userId))
        {
            return userId;
        }

        throw new HubException("The connection is not associated with a user.");
    }
}
