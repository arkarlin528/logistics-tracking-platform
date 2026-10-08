using Logistics.Application.Common;
using Logistics.Domain.Rates;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;
using Logistics.Domain.Users;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Logistics.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Password for the demo accounts. Set it in configuration; never hard-code a real one.</summary>
    public string? DemoPassword { get; set; }

    public int ShipmentCount { get; set; } = 40;

    /// <summary>Fixed seed so every fresh database tells the same story (useful for screenshots and tests).</summary>
    public int RandomSeed { get; set; } = 42;
}

/// <summary>
/// Fills an empty database with fake but realistic data: real port locations, invented companies,
/// and shipments at every stage of their journey relative to "now".
/// </summary>
public sealed class DataSeeder(
    AppDbContext db,
    IReferenceGenerator references,
    IPasswordHasher hasher,
    TimeProvider clock,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger)
{
    public const string AdminEmail = "admin@demo.test";
    public const string DispatcherEmail = "dispatcher@demo.test";
    public const string CustomerEmail = "customer@demo.test";

    private readonly SeedOptions _options = options.Value;

    public async Task MigrateAndSeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (await db.Ports.AnyAsync(ct))
            return;

        logger.LogInformation("Empty database, seeding demo data");

        // One transaction, so a failure halfway can't leave a half-seeded database that is then skipped forever.
        // Retries need the execution strategy to own the transaction (EnableRetryOnFailure is on).
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await SeedAllAsync(ct);
            await transaction.CommitAsync(ct);
        });
        logger.LogInformation("Seeded {Shipments} shipments", _options.ShipmentCount);
    }

    private async Task SeedAllAsync(CancellationToken ct)
    {
        var random = new Random(_options.RandomSeed);
        var now = clock.GetUtcNow();

        var ports = SeedPorts();
        var carriers = SeedCarriers();
        var customers = SeedCustomers();
        db.AddRange(ports);
        db.AddRange(carriers);
        db.AddRange(customers);
        await db.SaveChangesAsync(ct);

        await SeedUsersAsync(customers[0], ct);
        await SeedShipmentsAsync(random, now, ports, carriers, customers, ct);
        SeedRates(random, now, ports, carriers);
        await db.SaveChangesAsync(ct);
    }

    private static List<Port> SeedPorts() =>
    [
        new("THLCH", "Laem Chabang", "TH", 13.0800, 100.8830),
        new("THBKK", "Bangkok", "TH", 13.7000, 100.5700),
        new("SGSIN", "Singapore", "SG", 1.2640, 103.8400),
        new("MMRGN", "Yangon", "MM", 16.7700, 96.1700),
        new("VNSGN", "Ho Chi Minh City", "VN", 10.7700, 106.7100),
        new("MYPKG", "Port Klang", "MY", 3.0000, 101.3900),
        new("IDJKT", "Jakarta", "ID", -6.1000, 106.8800),
        new("CNSHA", "Shanghai", "CN", 31.2300, 121.4700),
        new("HKHKG", "Hong Kong", "HK", 22.2900, 114.1700),
        new("JPTYO", "Tokyo", "JP", 35.6200, 139.7700),
        new("NLRTM", "Rotterdam", "NL", 51.9500, 4.1400),
        new("USLAX", "Los Angeles", "US", 33.7300, -118.2600),
    ];

    // All company names are invented. They must not look like real carriers or customers.
    private static List<Carrier> SeedCarriers() =>
    [
        new("ASL", "Andaman Sea Lines", TransportMode.Sea),
        new("MCL", "Mekong Container Lines", TransportMode.Sea),
        new("SSC", "Straits Shipping Co.", TransportMode.Sea),
        new("SAC", "Siam Air Cargo", TransportMode.Air),
        new("GLT", "Golden Land Trucking", TransportMode.Road),
    ];

    private static List<Customer> SeedCustomers() =>
    [
        new("CPRE", "Chao Phraya Rice Exports", "ops@cpre.demo.test"),
        new("IRWT", "Irrawaddy Textiles", "logistics@irwt.demo.test"),
        new("LCEL", "Lion City Electronics", "supply@lcel.demo.test"),
        new("SGCT", "Saigon Coffee Traders", "export@sgct.demo.test"),
        new("KVAP", "Klang Valley Auto Parts", "shipping@kvap.demo.test"),
    ];

    private static readonly string[] Cargo =
    [
        "Jasmine rice, 20 pallets", "Cotton fabric rolls", "Consumer electronics, 2 containers",
        "Robusta coffee beans, 300 bags", "Automotive brake parts", "Frozen shrimp (reefer)",
        "Rubber sheets", "Solar panels", "Canned tuna", "Furniture, flat-packed",
    ];

    private static readonly string[] RoadPorts = ["THLCH", "THBKK", "MYPKG", "SGSIN", "MMRGN"];

    private async Task SeedUsersAsync(Customer demoCustomer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.DemoPassword))
        {
            logger.LogWarning("Seed:DemoPassword is not set, so no demo users were created");
            return;
        }

        var users = new[]
        {
            new User(AdminEmail, "Demo Admin", UserRole.Admin, null),
            new User(DispatcherEmail, "Demo Dispatcher", UserRole.Dispatcher, null),
            new User(CustomerEmail, "Demo Customer (Chao Phraya Rice)", UserRole.Customer, demoCustomer.Id),
        };
        foreach (var user in users)
            user.SetPasswordHash(hasher.Hash(user, _options.DemoPassword));

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedShipmentsAsync(Random random, DateTimeOffset now, List<Port> ports,
        List<Carrier> carriers, List<Customer> customers, CancellationToken ct)
    {
        for (var i = 0; i < _options.ShipmentCount; i++)
        {
            var carrier = carriers[random.Next(carriers.Count)];
            var lanePorts = carrier.Mode == TransportMode.Road
                ? ports.Where(p => RoadPorts.Contains(p.Code)).ToList()
                : ports;
            var origin = lanePorts[random.Next(lanePorts.Count)];
            Port destination;
            do destination = lanePorts[random.Next(lanePorts.Count)];
            while (destination == origin);

            var transit = TimeSpan.FromDays(carrier.Mode switch
            {
                TransportMode.Air => 1 + random.NextDouble() * 2,
                TransportMode.Road => 2 + random.NextDouble() * 3,
                _ => 6 + random.NextDouble() * 14,
            });
            // ~60% are still on their way (booked within their transit time), the rest are older history.
            var age = random.NextDouble() < 0.6
                ? transit * (0.05 + random.NextDouble() * 1.15)
                : transit * 1.5 + TimeSpan.FromDays(random.NextDouble() * 25);
            var createdAt = now - age;
            var eta = createdAt + transit;

            var shipment = Shipment.Create(await references.NextShipmentReferenceAsync(ct),
                customers[random.Next(customers.Count)], carrier, origin, destination,
                Cargo[random.Next(Cargo.Length)], Math.Round((decimal)(500 + random.NextDouble() * 24_500), 0),
                eta, createdAt);

            // ~30% run late: their journey takes up to 40% longer than planned.
            var pace = random.NextDouble() < 0.3 ? 1.1 + random.NextDouble() * 0.3 : 0.85 + random.NextDouble() * 0.15;
            var cancel = random.NextDouble() < 0.05;
            PlayJourney(shipment, createdAt, transit * pace, now, cancel);

            db.Shipments.Add(shipment);
        }
    }

    /// <summary>Records the events that would have happened by <paramref name="now"/> for a journey of the given length.</summary>
    internal static void PlayJourney(Shipment shipment, DateTimeOffset start, TimeSpan duration, DateTimeOffset now, bool cancel)
    {
        DateTimeOffset At(double fraction) => start + duration * fraction;
        bool Due(double fraction) => At(fraction) <= now;

        if (cancel)
        {
            if (Due(0.03))
                shipment.RecordEvent(ShipmentStatus.Cancelled, At(0.03), note: "Cancelled by customer");
            return;
        }

        if (!Due(0.05)) return;
        shipment.RecordEvent(ShipmentStatus.PickedUp, At(0.05), shipment.Origin.Name,
            shipment.Origin.Latitude, shipment.Origin.Longitude, "Cargo collected");

        // Position pings while moving. Straight-line (shorter way round) interpolation is good enough for a demo map.
        for (var f = 0.10; f <= 0.75; f += 0.13)
        {
            if (!Due(f)) return;
            var progress = (f - 0.10) / 0.75; // ends ~87% of the way; ArrivedAtPort puts it at the port
            var (lat, lng) = Interpolate(shipment.Origin, shipment.Destination, progress);
            shipment.RecordEvent(ShipmentStatus.InTransit, At(f), progress < 0.05 ? "Departed " + shipment.Origin.Name : "En route",
                lat, lng);
        }

        if (!Due(0.80)) return;
        shipment.RecordEvent(ShipmentStatus.ArrivedAtPort, At(0.80), shipment.Destination.Name,
            shipment.Destination.Latitude, shipment.Destination.Longitude);
        if (!Due(0.86)) return;
        shipment.RecordEvent(ShipmentStatus.CustomsClearance, At(0.86), shipment.Destination.Name, note: "Documents submitted");
        if (!Due(0.93)) return;
        shipment.RecordEvent(ShipmentStatus.OutForDelivery, At(0.93), shipment.Destination.Name);
        if (!Due(1.0)) return;
        shipment.RecordEvent(ShipmentStatus.Delivered, At(1.0), shipment.Destination.Name, note: "Signed by consignee");
    }

    internal static (double Lat, double Lng) Interpolate(Port from, Port to, double progress) =>
        GeoMath.Interpolate(from.Latitude, from.Longitude, to.Latitude, to.Longitude, progress);

    private void SeedRates(Random random, DateTimeOffset now, List<Port> ports, List<Carrier> carriers)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var validFrom = new DateOnly(today.Year, today.Month, 1);
        var validTo = validFrom.AddMonths(1).AddDays(-1);
        var hubs = ports.Where(p => p.Code is "THLCH" or "SGSIN" or "VNSGN").ToList();

        foreach (var origin in hubs)
        foreach (var destination in ports.Where(p => p != origin))
        foreach (var carrier in carriers.Where(c => c.Mode == TransportMode.Sea))
        {
            var distanceFactor = Math.Sqrt(Math.Pow(origin.Latitude - destination.Latitude, 2) +
                                           Math.Pow(origin.Longitude - destination.Longitude, 2));
            var base20 = Math.Round((decimal)(250 + distanceFactor * 28 + random.Next(-60, 60)), 0);
            foreach (var (type, multiplier) in new[] { ("20GP", 1.0m), ("40GP", 1.6m), ("40HC", 1.7m) })
            {
                db.FreightRates.Add(new FreightRate(origin, destination, carrier, type, "USD",
                    Math.Round(base20 * multiplier, 0), validFrom, validTo, "seed", now));
            }
        }
    }
}
