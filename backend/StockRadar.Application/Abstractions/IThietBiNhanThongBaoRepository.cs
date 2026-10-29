namespace StockRadar.Application.Abstractions;

/// <summary>Device persistence boundary; see specs/013-mobile-push-notify/plan.md for ownership and cleanup rules.</summary>
public interface IThietBiNhanThongBaoRepository
{
    /// <summary>Upserts a token and removes a different old token only when owned by this user.</summary>
    /// <param name="userId">Authenticated user's identifier.</param>
    /// <param name="token">Current device token; trimmed before lookup.</param>
    /// <param name="nenTang">Device platform validated by the application service.</param>
    /// <param name="tokenCu">Optional previous registration to remove if owned by this user.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>Completion after persisting the registration.</returns>
    Task DangKyAsync(Guid userId, string token, string nenTang, string? tokenCu = null,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotently removes a device token without checking account ownership.</summary>
    /// <param name="token">Registration to remove.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>Completion after persisting removal.</returns>
    Task XoaAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Removes only registrations confirmed unregistered by the push channel.</summary>
    /// <param name="tokens">Invalid registrations; other devices remain unchanged.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>Completion after persisting removal.</returns>
    Task XoaCacTokenAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default);

    /// <summary>Reads registrations belonging to one authenticated user.</summary>
    /// <param name="userId">Owner whose devices are requested.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The user's registered tokens.</returns>
    Task<IReadOnlyList<string>> LayTokenTheoUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Reads all registered devices for a broadcast batch.</summary>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>Tokens across every registered user.</returns>
    Task<IReadOnlyList<string>> LayTokenCacMayAsync(CancellationToken cancellationToken = default);
}
