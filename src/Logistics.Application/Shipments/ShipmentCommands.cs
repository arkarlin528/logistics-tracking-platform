using Logistics.Application.Common;
using Logistics.Domain.Common;
using Logistics.Domain.Shipments;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Shipments;

public sealed class ShipmentCommands(
    IAppDbContext db,
    IReferenceGenerator references,
    IShipmentNotifier notifier,
    TimeProvider clock)
{
    public async Task<ShipmentDetailDto> CreateAsync(CreateShipmentRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([request.CustomerId], ct)
            ?? throw new DomainException($"Customer {request.CustomerId} does not exist.");
        var carrier = await db.Carriers.FindAsync([request.CarrierId], ct)
            ?? throw new DomainException($"Carrier {request.CarrierId} does not exist.");
        var origin = await FindPortAsync(request.OriginPortCode, ct);
        var destination = await FindPortAsync(request.DestinationPortCode, ct);

        var now = clock.GetUtcNow();
        var reference = await references.NextShipmentReferenceAsync(ct);
        var shipment = Shipment.Create(reference, customer, carrier, origin, destination,
            request.CargoDescription, request.WeightKg, request.EstimatedArrival, now);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);
        await notifier.ShipmentUpdatedAsync(shipment.ToUpdatedMessage(shipment.Events[^1], now), ct);

        return shipment.ToDetailDto(now);
    }

    public async Task<ShipmentDetailDto> RecordEventAsync(int shipmentId, RecordEventRequest request, CancellationToken ct)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        var now = clock.GetUtcNow();

        var statusEvent = shipment.RecordEvent(request.Status, request.OccurredAt ?? now, request.Location,
            request.Latitude, request.Longitude, request.Note);

        // A concurrent update to the same shipment raises DbUpdateConcurrencyException (rowversion) -> HTTP 409.
        await db.SaveChangesAsync(ct);
        await notifier.ShipmentUpdatedAsync(shipment.ToUpdatedMessage(statusEvent, now), ct);

        return shipment.ToDetailDto(now);
    }

    public async Task<ShipmentDetailDto> ReviseEtaAsync(int shipmentId, ReviseEtaRequest request, CancellationToken ct)
    {
        var shipment = await LoadAsync(shipmentId, ct);
        var now = clock.GetUtcNow();

        var statusEvent = shipment.ReviseEstimatedArrival(request.EstimatedArrival, now, request.Reason);

        await db.SaveChangesAsync(ct);
        await notifier.ShipmentUpdatedAsync(shipment.ToUpdatedMessage(statusEvent, now), ct);

        return shipment.ToDetailDto(now);
    }

    private async Task<Shipment> LoadAsync(int id, CancellationToken ct) =>
        await db.Shipments
            .Include(s => s.Customer)
            .Include(s => s.Carrier)
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException($"Shipment {id} was not found.");

    private async Task<Domain.Reference.Port> FindPortAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await db.Ports.FirstOrDefaultAsync(p => p.Code == normalized, ct)
            ?? throw new DomainException($"Port {normalized} does not exist.");
    }
}
