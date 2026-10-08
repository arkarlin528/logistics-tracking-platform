using Logistics.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Infrastructure.Persistence;

internal sealed class SqlReferenceGenerator(AppDbContext db) : IReferenceGenerator
{
    public async Task<string> NextShipmentReferenceAsync(CancellationToken cancellationToken)
    {
        // A SQL Server SEQUENCE never hands out the same number twice, even under concurrent requests,
        // unlike "SELECT MAX(...) + 1". ToListAsync (not SingleAsync) because SQL Server doesn't allow
        // NEXT VALUE FOR inside the TOP(2) subquery EF would wrap around it.
        var next = await db.Database
            .SqlQueryRaw<int>($"SELECT NEXT VALUE FOR dbo.{AppDbContext.ShipmentNumberSequence} AS Value")
            .ToListAsync(cancellationToken);
        return $"SH-{next.Single()}";
    }
}
