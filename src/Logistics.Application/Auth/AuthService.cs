using FluentValidation;
using Logistics.Application.Common;
using Logistics.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(int Id, string Email, string DisplayName, UserRole Role, int? CustomerId);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, ICurrentUser currentUser)
{
    /// <returns>null when the email or password is wrong. The caller can't tell which, on purpose.</returns>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !hasher.Verify(user, request.Password))
            return null;

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<UserDto> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw new NotFoundException("User was not found.");
        return ToDto(user);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.CustomerId);
}
