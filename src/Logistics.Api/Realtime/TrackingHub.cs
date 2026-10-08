using Logistics.Api.Auth;
using Logistics.Application.Common;
using Logistics.Application.Shipments;
using Logistics.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Logistics.Api.Realtime;

/// <summary>Strongly-typed client contract, so a typo in a method name is a compile error, not a silent no-op.</summary>
public interface ITrackingClient
{
    Task ShipmentUpdated(ShipmentUpdatedMessage message);
}

/// <summary>
/// Clients only listen. Each connection joins one group on connect: staff see every shipment,
/// customers see only their own. The server decides the group from the JWT, so a customer
/// can't subscribe to someone else's shipments.
/// </summary>
[Authorize]
public sealed class TrackingHub : Hub<ITrackingClient>
{
    public const string Path = "/hubs/tracking";
    public const string StaffGroup = "staff";

    public static string CustomerGroup(int customerId) => $"customer-{customerId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is not null && Policies.StaffRoles.Any(user.IsInRole))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        }
        else if (user?.IsInRole(nameof(UserRole.Customer)) == true
                 && int.TryParse(user.FindFirst(AppClaims.CustomerId)?.Value, out var customerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomerGroup(customerId));
        }

        await base.OnConnectedAsync();
    }
}

internal sealed class SignalRShipmentNotifier(IHubContext<TrackingHub, ITrackingClient> hub) : IShipmentNotifier
{
    public Task ShipmentUpdatedAsync(ShipmentUpdatedMessage message, CancellationToken cancellationToken) =>
        hub.Clients
            .Groups(TrackingHub.StaffGroup, TrackingHub.CustomerGroup(message.CustomerId))
            .ShipmentUpdated(message);
}
