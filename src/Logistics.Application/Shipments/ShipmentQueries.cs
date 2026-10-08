using Logistics.Application.Common;
using Logistics.Domain.Shipments;
using Logistics.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Shipments;

public sealed class ShipmentQueries(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<PagedResult<ShipmentSummaryDto>> ListAsync(ShipmentFilter filter, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = VisibleShipments();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(s => s.Reference.Contains(term)
                || s.CargoDescription.Contains(term)
                || s.Customer.Name.Contains(term));
        }

        if (filter.Status is { } status)
            query = query.Where(s => s.Status == status);

        if (filter.CustomerId is { } customerId)
            query = query.Where(s => s.CustomerId == customerId);

        if (filter.Delayed is { } delayed)
        {
            query = delayed
                ? query.Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled
                    && s.EstimatedArrival < now)
                : query.Where(s => s.Status == ShipmentStatus.Delivered || s.Status == ShipmentStatus.Cancelled
                    || s.EstimatedArrival >= now);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new
            {
                s.Id, s.Reference, s.Status, s.Mode,
                CustomerName = s.Customer.Name,
                CarrierName = s.Carrier.Name,
                OriginCode = s.Origin.Code,
                DestinationCode = s.Destination.Code,
                s.EstimatedArrival, s.CurrentLatitude, s.CurrentLongitude,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ShipmentSummaryDto(
            r.Id, r.Reference, r.Status, r.Mode, r.CustomerName, r.CarrierName, r.OriginCode, r.DestinationCode,
            r.EstimatedArrival,
            IsDelayed: r.Status is not (ShipmentStatus.Delivered or ShipmentStatus.Cancelled) && now > r.EstimatedArrival,
            r.CurrentLatitude, r.CurrentLongitude)).ToList();

        return new PagedResult<ShipmentSummaryDto>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<ShipmentDetailDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var shipment = await WithDetails(VisibleShipments()).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Shipment {id} was not found.");
        return shipment.ToDetailDto(clock.GetUtcNow());
    }

    public async Task<ShipmentDetailDto> GetByReferenceAsync(string reference, CancellationToken ct)
    {
        var normalized = reference.Trim().ToUpperInvariant();
        var shipment = await WithDetails(VisibleShipments()).FirstOrDefaultAsync(s => s.Reference == normalized, ct)
            ?? throw new NotFoundException($"Shipment {normalized} was not found.");
        return shipment.ToDetailDto(clock.GetUtcNow());
    }

    /// <summary>Customers only ever see their own shipments. Enforced here, once, for every query.</summary>
    private IQueryable<Shipment> VisibleShipments()
    {
        var query = db.Shipments.AsNoTracking();
        return currentUser.Role == UserRole.Customer
            ? query.Where(s => s.CustomerId == currentUser.CustomerId)
            : query;
    }

    private static IQueryable<Shipment> WithDetails(IQueryable<Shipment> query) => query
        .Include(s => s.Customer)
        .Include(s => s.Carrier)
        .Include(s => s.Origin)
        .Include(s => s.Destination)
        .Include(s => s.Events);
}
