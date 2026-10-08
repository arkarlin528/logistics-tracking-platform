using Logistics.Domain.Common;
using Logistics.Domain.Reference;

namespace Logistics.Domain.Rates;

/// <summary>A carrier's price for one lane and container type, imported from the freight-data-pipeline project.</summary>
public sealed class FreightRate : Entity
{
    private FreightRate() { }

    public FreightRate(Port origin, Port destination, Carrier carrier, string containerType, string currency,
        decimal amount, DateOnly validFrom, DateOnly validTo, string source, DateTimeOffset importedAt)
    {
        if (origin.Code == destination.Code)
            throw new DomainException("Origin and destination must be different ports.");

        Origin = origin;
        OriginPortId = origin.Id;
        Destination = destination;
        DestinationPortId = destination.Id;
        Carrier = carrier;
        CarrierId = carrier.Id;
        ContainerType = containerType.ToUpperInvariant();
        ValidFrom = validFrom;
        Update(currency, amount, validTo, source, importedAt);
    }

    public int OriginPortId { get; private set; }
    public Port Origin { get; private set; } = default!;
    public int DestinationPortId { get; private set; }
    public Port Destination { get; private set; } = default!;
    public int CarrierId { get; private set; }
    public Carrier Carrier { get; private set; } = default!;
    /// <summary>20GP, 40GP, 40HC, LCL (per cbm) or AIR (per kg).</summary>
    public string ContainerType { get; private set; } = default!;
    public string Currency { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly ValidTo { get; private set; }
    public string Source { get; private set; } = default!;
    public DateTimeOffset ImportedAt { get; private set; }

    /// <summary>Used by the importer when the same rate arrives again (idempotent upsert).</summary>
    public void Update(string currency, decimal amount, DateOnly validTo, string source, DateTimeOffset importedAt)
    {
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");
        if (currency.Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO code.");
        if (validTo < ValidFrom)
            throw new DomainException("ValidTo must be on or after ValidFrom.");

        Currency = currency.ToUpperInvariant();
        Amount = amount;
        ValidTo = validTo;
        Source = source;
        ImportedAt = importedAt;
    }
}
