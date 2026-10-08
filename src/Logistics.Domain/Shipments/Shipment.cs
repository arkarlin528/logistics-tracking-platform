using Logistics.Domain.Common;
using Logistics.Domain.Reference;

namespace Logistics.Domain.Shipments;

/// <summary>
/// Aggregate root. All status changes go through <see cref="RecordEvent"/> so the
/// lifecycle rules in <see cref="ShipmentStatusTransitions"/> can't be bypassed.
/// </summary>
public sealed class Shipment : Entity
{
    private readonly List<StatusEvent> _events = [];

    private Shipment() { }

    public string Reference { get; private set; } = default!;
    public int CustomerId { get; private set; }
    public Customer Customer { get; private set; } = default!;
    public int CarrierId { get; private set; }
    public Carrier Carrier { get; private set; } = default!;
    public int OriginPortId { get; private set; }
    public Port Origin { get; private set; } = default!;
    public int DestinationPortId { get; private set; }
    public Port Destination { get; private set; } = default!;
    public TransportMode Mode { get; private set; }
    public string CargoDescription { get; private set; } = default!;
    public decimal WeightKg { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset EstimatedArrival { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public double? CurrentLatitude { get; private set; }
    public double? CurrentLongitude { get; private set; }

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<StatusEvent> Events => _events;

    public static Shipment Create(string reference, Customer customer, Carrier carrier, Port origin,
        Port destination, string cargoDescription, decimal weightKg, DateTimeOffset estimatedArrival,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("Reference is required.");
        if (origin.Code == destination.Code)
            throw new DomainException("Origin and destination must be different ports.");
        if (weightKg <= 0)
            throw new DomainException("Weight must be greater than zero.");
        if (estimatedArrival <= now)
            throw new DomainException("Estimated arrival must be in the future.");

        var shipment = new Shipment
        {
            Reference = reference,
            Customer = customer,
            CustomerId = customer.Id,
            Carrier = carrier,
            CarrierId = carrier.Id,
            Origin = origin,
            OriginPortId = origin.Id,
            Destination = destination,
            DestinationPortId = destination.Id,
            Mode = carrier.Mode,
            CargoDescription = cargoDescription.Trim(),
            WeightKg = weightKg,
            Status = ShipmentStatus.Booked,
            CreatedAt = now,
            EstimatedArrival = estimatedArrival,
            CurrentLatitude = origin.Latitude,
            CurrentLongitude = origin.Longitude,
        };
        shipment._events.Add(new StatusEvent(ShipmentStatus.Booked, now, origin.Name,
            origin.Latitude, origin.Longitude, "Shipment booked"));
        return shipment;
    }

    public StatusEvent RecordEvent(ShipmentStatus newStatus, DateTimeOffset occurredAt, string? location = null,
        double? latitude = null, double? longitude = null, string? note = null)
    {
        if (!ShipmentStatusTransitions.CanMove(Status, newStatus))
            throw new DomainException($"Cannot move shipment {Reference} from {Status} to {newStatus}.");
        if (_events.Count > 0 && occurredAt < _events[^1].OccurredAt)
            throw new DomainException("Events must be recorded in time order.");
        if ((latitude is null) != (longitude is null))
            throw new DomainException("Latitude and longitude must be given together.");

        var statusEvent = new StatusEvent(newStatus, occurredAt, location, latitude, longitude, note);
        _events.Add(statusEvent);
        Status = newStatus;

        if (latitude is not null)
        {
            CurrentLatitude = latitude;
            CurrentLongitude = longitude;
        }

        if (newStatus == ShipmentStatus.Delivered)
        {
            DeliveredAt = occurredAt;
            CurrentLatitude = Destination.Latitude;
            CurrentLongitude = Destination.Longitude;
        }

        return statusEvent;
    }

    /// <summary>Moves the ETA, e.g. after a port congestion delay. Logged as a note on the current status.</summary>
    public StatusEvent ReviseEstimatedArrival(DateTimeOffset newEta, DateTimeOffset now, string reason)
    {
        if (ShipmentStatusTransitions.IsFinal(Status))
            throw new DomainException($"Shipment {Reference} is {Status}; its ETA can't change.");
        if (newEta <= now)
            throw new DomainException("Estimated arrival must be in the future.");

        var previous = EstimatedArrival;
        EstimatedArrival = newEta;
        var statusEvent = new StatusEvent(Status, now, null, null, null,
            $"ETA changed from {previous:yyyy-MM-dd} to {newEta:yyyy-MM-dd}: {reason}");
        _events.Add(statusEvent);
        return statusEvent;
    }

    public bool IsDelayed(DateTimeOffset now) =>
        !ShipmentStatusTransitions.IsFinal(Status) && now > EstimatedArrival;
}
