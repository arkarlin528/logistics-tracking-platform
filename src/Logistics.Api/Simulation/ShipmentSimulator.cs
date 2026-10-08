using Logistics.Application.Common;
using Logistics.Application.Shipments;
using Logistics.Domain.Common;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Logistics.Api.Simulation;

public sealed class SimulationOptions
{
    public const string Section = "Simulation";

    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Book a new shipment every N ticks so the demo never runs out of moving shipments.</summary>
    public int NewShipmentEveryTicks { get; set; } = 12;
}

/// <summary>
/// Stands in for real carrier/GPS feeds in the demo: every few seconds it moves one active shipment
/// a step forward through the normal application commands, so validation, persistence and the SignalR
/// push all run exactly as they would for a real update.
/// </summary>
internal sealed class ShipmentSimulator(
    IServiceScopeFactory scopes,
    IOptions<SimulationOptions> options,
    TimeProvider clock,
    ILogger<ShipmentSimulator> logger) : BackgroundService
{
    private const double StepPerTick = 0.15;
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return;

        logger.LogInformation("Shipment simulator running every {Seconds}s", settings.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds), clock);
        var tick = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (++tick % settings.NewShipmentEveryTicks == 0)
                    await BookRandomShipmentAsync(scope.ServiceProvider, stoppingToken);
                else
                    await AdvanceRandomShipmentAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is DomainException or DbUpdateConcurrencyException)
            {
                // Someone changed the shipment at the same moment; skip this tick.
                logger.LogDebug(ex, "Simulator step skipped");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Simulator step failed");
            }
        }
    }

    private async Task AdvanceRandomShipmentAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IAppDbContext>();
        var commands = services.GetRequiredService<ShipmentCommands>();

        var active = await db.Shipments.AsNoTracking()
            .Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled)
            .Select(s => new { s.Id, s.Status, s.CurrentLatitude, s.CurrentLongitude, s.Origin, s.Destination })
            .ToListAsync(ct);
        if (active.Count == 0)
            return;

        var s = active[_random.Next(active.Count)];
        var request = s.Status switch
        {
            ShipmentStatus.Booked => Event(ShipmentStatus.PickedUp, s.Origin.Name, s.Origin, "Cargo collected"),
            ShipmentStatus.PickedUp => Event(ShipmentStatus.InTransit, "Departed " + s.Origin.Name, s.Origin),
            ShipmentStatus.InTransit => NextLeg(s.Origin, s.Destination, s.CurrentLatitude, s.CurrentLongitude),
            ShipmentStatus.ArrivedAtPort => Event(ShipmentStatus.CustomsClearance, s.Destination.Name, null, "Documents submitted"),
            ShipmentStatus.CustomsClearance => Event(ShipmentStatus.OutForDelivery, s.Destination.Name),
            ShipmentStatus.OutForDelivery => Event(ShipmentStatus.Delivered, s.Destination.Name, null, "Signed by consignee"),
            _ => null,
        };

        if (request is null)
            return;

        if (s.Status == ShipmentStatus.InTransit && _random.NextDouble() < 0.08)
        {
            await commands.ReviseEtaAsync(s.Id, new ReviseEtaRequest(
                clock.GetUtcNow().AddDays(1 + _random.Next(3)), "Weather delay reported by carrier"), ct);
            return;
        }

        await commands.RecordEventAsync(s.Id, request, ct);
    }

    private static RecordEventRequest NextLeg(Port origin, Port destination, double? lat, double? lng)
    {
        var total = GeoMath.Distance(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude);
        var remaining = GeoMath.Distance(lat ?? origin.Latitude, lng ?? origin.Longitude, destination.Latitude, destination.Longitude);
        var progress = total == 0 ? 1 : 1 - remaining / total;
        var next = progress + StepPerTick;

        if (next >= 0.95)
            return Event(ShipmentStatus.ArrivedAtPort, destination.Name, destination);

        var (nextLat, nextLng) = GeoMath.Interpolate(origin.Latitude, origin.Longitude,
            destination.Latitude, destination.Longitude, next);
        return new RecordEventRequest(ShipmentStatus.InTransit, null, "En route", nextLat, nextLng, null);
    }

    private static RecordEventRequest Event(ShipmentStatus status, string location, Port? at = null, string? note = null) =>
        new(status, null, location, at?.Latitude, at?.Longitude, note);

    private async Task BookRandomShipmentAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IAppDbContext>();
        var commands = services.GetRequiredService<ShipmentCommands>();

        var customers = await db.Customers.AsNoTracking().Select(c => c.Id).ToListAsync(ct);
        var carriers = await db.Carriers.AsNoTracking().Where(c => c.Mode != TransportMode.Road).ToListAsync(ct);
        var ports = await db.Ports.AsNoTracking().Select(p => p.Code).ToListAsync(ct);
        if (customers.Count == 0 || carriers.Count == 0 || ports.Count < 2)
            return;

        var carrier = carriers[_random.Next(carriers.Count)];
        var origin = ports[_random.Next(ports.Count)];
        var destination = ports.Where(p => p != origin).ElementAt(_random.Next(ports.Count - 1));
        var transitDays = carrier.Mode == TransportMode.Air ? 2 : 7 + _random.Next(10);

        var created = await commands.CreateAsync(new CreateShipmentRequest(
            customers[_random.Next(customers.Count)], carrier.Id, origin, destination,
            "Mixed general cargo", 1000 + _random.Next(20_000), clock.GetUtcNow().AddDays(transitDays)), ct);
        logger.LogInformation("Simulator booked {Reference}", created.Reference);
    }
}
