namespace StockRadar.Application.Abstractions;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}

public interface IUserRepository
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateAsync(string email, string passwordHash, string displayName, CancellationToken cancellationToken = default);
    Task EnsureGuestUserAsync(CancellationToken cancellationToken = default);
    Task EnsureAdminUserAsync(string passwordHash, CancellationToken cancellationToken = default);
}

public sealed record UserAccount(Guid Id, string Email, string PasswordHash, string DisplayName);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public sealed record AuthResult(Guid UserId, string Email, string DisplayName, string Token);

public interface ITokenService
{
    string CreateToken(UserAccount user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
