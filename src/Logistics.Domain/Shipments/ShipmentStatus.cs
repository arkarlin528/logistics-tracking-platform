namespace Logistics.Domain.Shipments;

public enum ShipmentStatus
{
    Booked = 1,
    PickedUp = 2,
    InTransit = 3,
    ArrivedAtPort = 4,
    CustomsClearance = 5,
    OutForDelivery = 6,
    Delivered = 7,
    Cancelled = 8,
}
