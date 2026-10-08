using Logistics.Domain.Common;

namespace Logistics.Domain.Users;

public sealed class User : Entity
{
    private User() { }

    public User(string email, string displayName, UserRole role, int? customerId)
    {
        if (role == UserRole.Customer && customerId is null)
            throw new DomainException("Customer users must belong to a customer.");
        if (role != UserRole.Customer && customerId is not null)
            throw new DomainException("Only customer users can belong to a customer.");

        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName;
        Role = role;
        CustomerId = customerId;
    }

    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public int? CustomerId { get; private set; }

    public void SetPasswordHash(string hash) => PasswordHash = hash;
}
