using Logistics.Application.Common;
using Logistics.Domain.Rates;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;
using Logistics.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public const string ShipmentNumberSequence = "ShipmentNumbers";

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<StatusEvent> StatusEvents => Set<StatusEvent>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<FreightRate> FreightRates => Set<FreightRate>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(ShipmentNumberSequence).StartsAt(100001);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
