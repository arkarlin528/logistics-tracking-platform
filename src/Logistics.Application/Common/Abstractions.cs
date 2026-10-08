using Logistics.Domain.Rates;
using Logistics.Domain.Reference;
using Logistics.Domain.Shipments;
using Logistics.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Common;

/// <summary>
/// EF Core is used directly instead of hiding it behind repositories: DbSet already is a
/// repository and unit of work, and keeping IQueryable lets queries project straight to DTOs.
/// See docs/adr/0001-clean-architecture.md.
/// </summary>
public interface IAppDbContext
{
    DbSet<Shipment> Shipments { get; }
    DbSet<StatusEvent> StatusEvents { get; }
    DbSet<Port> Ports { get; }
    DbSet<Carrier> Carriers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<FreightRate> FreightRates { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The signed-in caller, read from the JWT.</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    UserRole? Role { get; }
    int? CustomerId { get; }
}

/// <summary>Pushes shipment changes to connected clients. Implemented with SignalR in the API.</summary>
public interface IShipmentNotifier
{
    Task ShipmentUpdatedAsync(Shipments.ShipmentUpdatedMessage message, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}

public interface IReferenceGenerator
{
    /// <summary>Next shipment reference such as SH-100042. Backed by a database sequence so it's safe under concurrency.</summary>
    Task<string> NextShipmentReferenceAsync(CancellationToken cancellationToken);
}

public static class CurrentUserExtensions
{
    public static bool IsStaff(this ICurrentUser user) => user.Role is UserRole.Admin or UserRole.Dispatcher;
}
