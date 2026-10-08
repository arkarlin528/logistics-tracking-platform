using FluentValidation;
using Logistics.Application.Common;
using Logistics.Domain.Common;
using Logistics.Domain.Rates;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Rates;

public sealed record FreightRateDto(
    int Id,
    string OriginCode,
    string DestinationCode,
    string CarrierCode,
    string CarrierName,
    string ContainerType,
    string Currency,
    decimal Amount,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Source);

public sealed record RateSearch(string? Origin, string? Destination, DateOnly? On);

public sealed record RateImportRow(
    string Origin,
    string Destination,
    string CarrierCode,
    string ContainerType,
    string Currency,
    decimal Amount,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public sealed record RateImportRequest(string Source, IReadOnlyList<RateImportRow> Rates);

public sealed record RateImportRejection(int Row, string Reason);

public sealed record RateImportResult(int Inserted, int Updated, IReadOnlyList<RateImportRejection> Rejected);

public sealed class RateImportRequestValidator : AbstractValidator<RateImportRequest>
{
    public const int MaxRows = 1000;

    public RateImportRequestValidator()
    {
        RuleFor(x => x.Source).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rates).NotEmpty();
        RuleFor(x => x.Rates.Count).LessThanOrEqualTo(MaxRows).WithName("Rates")
            .WithMessage($"Send at most {MaxRows} rates per request.");
    }
}

public sealed class RateService(IAppDbContext db, TimeProvider clock)
{
    public async Task<List<FreightRateDto>> SearchAsync(RateSearch search, CancellationToken ct)
    {
        var on = search.On ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var query = db.FreightRates.AsNoTracking().Where(r => r.ValidFrom <= on && r.ValidTo >= on);

        if (!string.IsNullOrWhiteSpace(search.Origin))
            query = query.Where(r => r.Origin.Code == search.Origin.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(search.Destination))
            query = query.Where(r => r.Destination.Code == search.Destination.Trim().ToUpper());

        return await query
            .OrderBy(r => r.Origin.Code).ThenBy(r => r.Destination.Code).ThenBy(r => r.Amount)
            .Take(200)
            .Select(r => new FreightRateDto(r.Id, r.Origin.Code, r.Destination.Code, r.Carrier.Code, r.Carrier.Name,
                r.ContainerType, r.Currency, r.Amount, r.ValidFrom, r.ValidTo, r.Source))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Idempotent upsert: the same (lane, carrier, container type, valid-from) updates the existing row
    /// instead of adding a duplicate, so the pipeline can safely retry a whole batch.
    /// Bad rows are reported back and the good ones are still saved.
    /// </summary>
    public async Task<RateImportResult> ImportAsync(RateImportRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ports = await db.Ports.ToDictionaryAsync(p => p.Code, ct);
        var carriers = await db.Carriers.ToDictionaryAsync(c => c.Code, ct);

        var inserted = 0;
        var updated = 0;
        var rejected = new List<RateImportRejection>();
        var seenInBatch = new HashSet<(string, string, string, string, DateOnly)>();

        for (var i = 0; i < request.Rates.Count; i++)
        {
            var row = request.Rates[i];
            var originCode = row.Origin.Trim().ToUpperInvariant();
            var destinationCode = row.Destination.Trim().ToUpperInvariant();
            var carrierCode = row.CarrierCode.Trim().ToUpperInvariant();
            var containerType = row.ContainerType.Trim().ToUpperInvariant();

            if (!ports.TryGetValue(originCode, out var origin))
            {
                rejected.Add(new(i, $"Unknown origin port '{originCode}'."));
                continue;
            }
            if (!ports.TryGetValue(destinationCode, out var destination))
            {
                rejected.Add(new(i, $"Unknown destination port '{destinationCode}'."));
                continue;
            }
            if (!carriers.TryGetValue(carrierCode, out var carrier))
            {
                rejected.Add(new(i, $"Unknown carrier '{carrierCode}'."));
                continue;
            }
            if (!seenInBatch.Add((originCode, destinationCode, carrierCode, containerType, row.ValidFrom)))
            {
                rejected.Add(new(i, "Duplicate of an earlier row in this batch."));
                continue;
            }

            try
            {
                var existing = await db.FreightRates.FirstOrDefaultAsync(r =>
                    r.OriginPortId == origin.Id && r.DestinationPortId == destination.Id &&
                    r.CarrierId == carrier.Id && r.ContainerType == containerType &&
                    r.ValidFrom == row.ValidFrom, ct);

                if (existing is null)
                {
                    db.FreightRates.Add(new FreightRate(origin, destination, carrier, containerType, row.Currency,
                        row.Amount, row.ValidFrom, row.ValidTo, request.Source, now));
                    inserted++;
                }
                else
                {
                    existing.Update(row.Currency, row.Amount, row.ValidTo, request.Source, now);
                    updated++;
                }
            }
            catch (DomainException ex)
            {
                rejected.Add(new(i, ex.Message));
            }
        }

        await db.SaveChangesAsync(ct);
        return new RateImportResult(inserted, updated, rejected);
    }
}
