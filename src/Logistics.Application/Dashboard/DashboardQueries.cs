using Logistics.Application.Common;
using Logistics.Domain.Shipments;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Dashboard;

public sealed record StatusCount(ShipmentStatus Status, int Count);

public sealed record DelayedShipment(int Id, string Reference, string CustomerName, DateTimeOffset EstimatedArrival, ShipmentStatus Status);

public sealed record DashboardSummary(
    int Active,
    int Delayed,
    int DeliveredLast7Days,
    double OnTimeRateLast30Days,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<DelayedShipment> MostDelayed);

public sealed class DashboardQueries(IAppDbContext db, TimeProvider clock)
{
    public async Task<DashboardSummary> SummaryAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var shipments = db.Shipments.AsNoTracking();
        var active = shipments.Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled);

        var byStatus = await shipments
            .GroupBy(s => s.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var activeCount = await active.CountAsync(ct);
        var delayedQuery = active.Where(s => s.EstimatedArrival < now);
        var delayedCount = await delayedQuery.CountAsync(ct);
        var mostDelayed = await delayedQuery
            .OrderBy(s => s.EstimatedArrival)
            .Take(5)
            .Select(s => new DelayedShipment(s.Id, s.Reference, s.Customer.Name, s.EstimatedArrival, s.Status))
            .ToListAsync(ct);

        var weekAgo = now.AddDays(-7);
        var deliveredLastWeek = await shipments.CountAsync(s => s.DeliveredAt >= weekAgo, ct);

        var monthAgo = now.AddDays(-30);
        var deliveredLastMonth = shipments.Where(s => s.DeliveredAt >= monthAgo);
        var deliveredCount = await deliveredLastMonth.CountAsync(ct);
        var onTimeCount = await deliveredLastMonth.CountAsync(s => s.DeliveredAt <= s.EstimatedArrival, ct);
        var onTimeRate = deliveredCount == 0 ? 1d : Math.Round(onTimeCount / (double)deliveredCount, 3);

        return new DashboardSummary(activeCount, delayedCount, deliveredLastWeek, onTimeRate,
            byStatus.OrderBy(s => s.Status).ToList(), mostDelayed);
    }
}
