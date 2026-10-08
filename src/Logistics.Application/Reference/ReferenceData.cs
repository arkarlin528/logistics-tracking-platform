using Logistics.Application.Common;
using Logistics.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Reference;

public sealed record PortDto(int Id, string Code, string Name, string Country, double Latitude, double Longitude);

public sealed record CarrierDto(int Id, string Code, string Name, TransportMode Mode);

public sealed record CustomerDto(int Id, string Code, string Name, string ContactEmail);

internal static class ReferenceMapping
{
    public static PortDto ToDto(this Port p) => new(p.Id, p.Code, p.Name, p.Country, p.Latitude, p.Longitude);
}

public sealed class ReferenceDataQueries(IAppDbContext db)
{
    public Task<List<PortDto>> PortsAsync(CancellationToken ct) => db.Ports.AsNoTracking()
        .OrderBy(p => p.Code)
        .Select(p => new PortDto(p.Id, p.Code, p.Name, p.Country, p.Latitude, p.Longitude))
        .ToListAsync(ct);

    public Task<List<CarrierDto>> CarriersAsync(CancellationToken ct) => db.Carriers.AsNoTracking()
        .OrderBy(c => c.Name)
        .Select(c => new CarrierDto(c.Id, c.Code, c.Name, c.Mode))
        .ToListAsync(ct);

    public Task<List<CustomerDto>> CustomersAsync(CancellationToken ct) => db.Customers.AsNoTracking()
        .OrderBy(c => c.Name)
        .Select(c => new CustomerDto(c.Id, c.Code, c.Name, c.ContactEmail))
        .ToListAsync(ct);
}
