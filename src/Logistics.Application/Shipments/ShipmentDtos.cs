using Logistics.Application.Reference;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;

namespace Logistics.Application.Shipments;

public sealed record ShipmentSummaryDto(
    int Id,
    string Reference,
    ShipmentStatus Status,
    TransportMode Mode,
    string CustomerName,
    string CarrierName,
    string OriginCode,
    string DestinationCode,
    DateTimeOffset EstimatedArrival,
    bool IsDelayed,
    double? CurrentLatitude,
    double? CurrentLongitude);

public sealed record StatusEventDto(
    int Id,
    ShipmentStatus Status,
    DateTimeOffset OccurredAt,
    string? Location,
    double? Latitude,
    double? Longitude,
    string? Note);

public sealed record ShipmentDetailDto(
    int Id,
    string Reference,
    ShipmentStatus Status,
    TransportMode Mode,
    int CustomerId,
    string CustomerName,
    string CarrierName,
    PortDto Origin,
    PortDto Destination,
    string CargoDescription,
    decimal WeightKg,
    DateTimeOffset CreatedAt,
    DateTimeOffset EstimatedArrival,
    DateTimeOffset? DeliveredAt,
    bool IsDelayed,
    double? CurrentLatitude,
    double? CurrentLongitude,
    IReadOnlyList<ShipmentStatus> NextStatuses,
    IReadOnlyList<StatusEventDto> Events);

public sealed record ShipmentFilter(
    string? Search = null,
    ShipmentStatus? Status = null,
    bool? Delayed = null,
    int? CustomerId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateShipmentRequest(
    int CustomerId,
    int CarrierId,
    string OriginPortCode,
    string DestinationPortCode,
    string CargoDescription,
    decimal WeightKg,
    DateTimeOffset EstimatedArrival);

public sealed record RecordEventRequest(
    ShipmentStatus Status,
    DateTimeOffset? OccurredAt,
    string? Location,
    double? Latitude,
    double? Longitude,
    string? Note);

public sealed record ReviseEtaRequest(DateTimeOffset EstimatedArrival, string Reason);

/// <summary>Payload pushed over SignalR whenever a shipment changes.</summary>
public sealed record ShipmentUpdatedMessage(
    int ShipmentId,
    string Reference,
    int CustomerId,
    ShipmentStatus Status,
    bool IsDelayed,
    DateTimeOffset EstimatedArrival,
    double? CurrentLatitude,
    double? CurrentLongitude,
    StatusEventDto LatestEvent);

internal static class ShipmentMapping
{
    public static StatusEventDto ToDto(this StatusEvent e) =>
        new(e.Id, e.Status, e.OccurredAt, e.Location, e.Latitude, e.Longitude, e.Note);

    public static ShipmentDetailDto ToDetailDto(this Shipment s, DateTimeOffset now) => new(
        s.Id, s.Reference, s.Status, s.Mode, s.CustomerId, s.Customer.Name, s.Carrier.Name,
        s.Origin.ToDto(), s.Destination.ToDto(), s.CargoDescription, s.WeightKg, s.CreatedAt,
        s.EstimatedArrival, s.DeliveredAt, s.IsDelayed(now), s.CurrentLatitude, s.CurrentLongitude,
        ShipmentStatusTransitions.NextFrom(s.Status),
        s.Events.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.ToDto()).ToList());

    public static ShipmentUpdatedMessage ToUpdatedMessage(this Shipment s, StatusEvent latest, DateTimeOffset now) => new(
        s.Id, s.Reference, s.CustomerId, s.Status, s.IsDelayed(now), s.EstimatedArrival,
        s.CurrentLatitude, s.CurrentLongitude, latest.ToDto());
}
