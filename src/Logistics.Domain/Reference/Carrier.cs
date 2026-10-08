using Logistics.Domain.Common;

namespace Logistics.Domain.Reference;

public sealed class Carrier : Entity
{
    private Carrier() { }

    public Carrier(string code, string name, TransportMode mode)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        Mode = mode;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public TransportMode Mode { get; private set; }
}
