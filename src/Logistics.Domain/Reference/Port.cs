using Logistics.Domain.Common;

namespace Logistics.Domain.Reference;

public sealed class Port : Entity
{
    private Port() { }

    public Port(string code, string name, string country, double latitude, double longitude)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        Country = country;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>UN/LOCODE, e.g. THLCH (Laem Chabang).</summary>
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
}
