using Logistics.Domain.Users;

namespace Logistics.Api.Auth;

public static class Policies
{
    /// <summary>Admins and dispatchers: can see every customer's shipments and change them.</summary>
    public const string Staff = nameof(Staff);

    public static readonly string[] StaffRoles = [nameof(UserRole.Admin), nameof(UserRole.Dispatcher)];
}
