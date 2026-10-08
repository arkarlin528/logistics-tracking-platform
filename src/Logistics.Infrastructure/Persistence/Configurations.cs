using Logistics.Domain.Rates;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;
using Logistics.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Logistics.Infrastructure.Persistence;

// Enums are stored as strings: a few bytes more per row, but the data is readable in SSMS and
// adding a new enum value can never silently change the meaning of existing rows.

internal sealed class PortConfiguration : IEntityTypeConfiguration<Port>
{
    public void Configure(EntityTypeBuilder<Port> b)
    {
        b.Property(p => p.Code).HasMaxLength(5).IsUnicode(false);
        b.Property(p => p.Name).HasMaxLength(100);
        b.Property(p => p.Country).HasMaxLength(2).IsUnicode(false);
        b.HasIndex(p => p.Code).IsUnique();
    }
}

internal sealed class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> b)
    {
        b.Property(c => c.Code).HasMaxLength(10).IsUnicode(false);
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.Mode).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.Code).HasMaxLength(10).IsUnicode(false);
        b.Property(c => c.Name).HasMaxLength(150);
        b.Property(c => c.ContactEmail).HasMaxLength(200);
        b.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.Property(s => s.Reference).HasMaxLength(20).IsUnicode(false);
        b.HasIndex(s => s.Reference).IsUnique();

        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Mode).HasConversion<string>().HasMaxLength(10);
        b.Property(s => s.CargoDescription).HasMaxLength(500);
        b.Property(s => s.WeightKg).HasPrecision(12, 2);
        b.Property(s => s.RowVersion).IsRowVersion();

        b.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Carrier).WithMany().HasForeignKey(s => s.CarrierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Origin).WithMany().HasForeignKey(s => s.OriginPortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Destination).WithMany().HasForeignKey(s => s.DestinationPortId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(s => s.Events).WithOne().HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(s => s.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Supports the dashboard and the "delayed" filter.
        b.HasIndex(s => new { s.Status, s.EstimatedArrival });
        b.HasIndex(s => new { s.CustomerId, s.CreatedAt });
    }
}

internal sealed class StatusEventConfiguration : IEntityTypeConfiguration<StatusEvent>
{
    public void Configure(EntityTypeBuilder<StatusEvent> b)
    {
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.Location).HasMaxLength(200);
        b.Property(e => e.Note).HasMaxLength(500);
        b.HasIndex(e => new { e.ShipmentId, e.OccurredAt });
    }
}

internal sealed class FreightRateConfiguration : IEntityTypeConfiguration<FreightRate>
{
    public void Configure(EntityTypeBuilder<FreightRate> b)
    {
        b.Property(r => r.ContainerType).HasMaxLength(10).IsUnicode(false);
        b.Property(r => r.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        b.Property(r => r.Amount).HasPrecision(12, 2);
        b.Property(r => r.Source).HasMaxLength(100);

        b.HasOne(r => r.Origin).WithMany().HasForeignKey(r => r.OriginPortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Destination).WithMany().HasForeignKey(r => r.DestinationPortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Carrier).WithMany().HasForeignKey(r => r.CarrierId).OnDelete(DeleteBehavior.Restrict);

        // The natural key the importer upserts on. Unique, so even a racing import can't create duplicates.
        b.HasIndex(r => new { r.OriginPortId, r.DestinationPortId, r.CarrierId, r.ContainerType, r.ValidFrom })
            .IsUnique();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Email).HasMaxLength(200);
        b.Property(u => u.DisplayName).HasMaxLength(100);
        b.Property(u => u.PasswordHash).HasMaxLength(200);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasOne<Customer>().WithMany().HasForeignKey(u => u.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
