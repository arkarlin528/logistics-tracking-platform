namespace Logistics.Domain.Shipments;

/// <summary>
/// The shipment lifecycle as a state machine. Keeping the rules in one table makes them
/// easy to read, test and show in the README.
/// </summary>
public static class ShipmentStatusTransitions
{
    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]> Allowed = new()
    {
        [ShipmentStatus.Booked] = [ShipmentStatus.PickedUp, ShipmentStatus.Cancelled],
        [ShipmentStatus.PickedUp] = [ShipmentStatus.InTransit, ShipmentStatus.Cancelled],
        // InTransit -> InTransit is a position update.
        [ShipmentStatus.InTransit] = [ShipmentStatus.InTransit, ShipmentStatus.ArrivedAtPort],
        // ArrivedAtPort -> InTransit covers transshipment via a hub port.
        [ShipmentStatus.ArrivedAtPort] = [ShipmentStatus.CustomsClearance, ShipmentStatus.InTransit],
        [ShipmentStatus.CustomsClearance] = [ShipmentStatus.OutForDelivery],
        [ShipmentStatus.OutForDelivery] = [ShipmentStatus.Delivered],
        [ShipmentStatus.Delivered] = [],
        [ShipmentStatus.Cancelled] = [],
    };

    public static bool CanMove(ShipmentStatus from, ShipmentStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<ShipmentStatus> NextFrom(ShipmentStatus from) => Allowed[from];

    public static bool IsFinal(ShipmentStatus status) => Allowed[status].Length == 0;
}
