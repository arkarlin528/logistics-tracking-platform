using Logistics.Domain.Common;

namespace Logistics.Domain.Reference;

public sealed class Customer : Entity
{
    private Customer() { }

    public Customer(string code, string name, string contactEmail)
    {
        Code = code.ToUpperInvariant();
        Name = name;
        ContactEmail = contactEmail;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string ContactEmail { get; private set; } = default!;
}
