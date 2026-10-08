using Logistics.Domain.Common;

namespace Logistics.Domain.Shipments;

public sealed class StatusEvent : Entity
{
    private StatusEvent() { }

    internal StatusEvent(ShipmentStatus status, DateTimeOffset occurredAt, string? location,
        double? latitude, double? longitude, string? note)
    {
        Status = status;
        OccurredAt = occurredAt;
        Location = location;
        Latitude = latitude;
        Longitude = longitude;
        Note = note;
    }

    public int ShipmentId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Location { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? Note { get; private set; }
}
