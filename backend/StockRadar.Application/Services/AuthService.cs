using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;

namespace StockRadar.Application.Services;
public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await users.FindByEmailAsync(normalized, cancellationToken);
        if (existing is not null)
            throw new AppException("Conflict", "Email đã được sử dụng.", 409);

        var hash = passwordHasher.Hash(password);
        var user = await users.CreateAsync(normalized, hash, displayName.Trim(), cancellationToken);
        var token = tokenService.CreateToken(user);

        return new AuthResult(user.Id, user.Email, user.DisplayName, token);
    }

    public async Task<AuthResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(normalized, cancellationToken);
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
            return null;

        var token = tokenService.CreateToken(user);
        return new AuthResult(user.Id, user.Email, user.DisplayName, token);
    }
}
