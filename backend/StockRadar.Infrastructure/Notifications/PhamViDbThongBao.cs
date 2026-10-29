using Microsoft.Extensions.DependencyInjection;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Owns isolated notification database scopes and one process-wide operation slot per service provider.</summary>
internal sealed class PhamViDbThongBao(IServiceScopeFactory taoPhamVi)
{
    private readonly SemaphoreSlim slot = new(1, 1);

    /// <summary>Waits for the shared slot, then starts a read or cleanup without borrowing any runner-scoped service.</summary>
    /// <param name="thaoTac">Database operation that owns its scope until actual completion.</param>
    /// <param name="huy">Cooperative cancellation, independent from the caller's bounded wait.</param>
    /// <typeparam name="TKetQua">Materialized result; no context-backed deferred query may escape.</typeparam>
    /// <returns>The entire operation, including asynchronous scope disposal.</returns>
    /// <exception cref="OperationCanceledException">Another operation still held the slot when the caller's budget expired.</exception>
    internal async Task<TKetQua> ChayAsync<TKetQua>(Func<IThietBiNhanThongBaoRepository, CancellationToken, Task<TKetQua>> thaoTac,
        CancellationToken huy)
    {
        huy.ThrowIfCancellationRequested();
        // LIMIT: Single slot per service provider; a competing flush waits within the channel's Push:TimeoutGiay budget.
        // If the budget expires first, that batch skips push with qua_han.
        await slot.WaitAsync(huy).ConfigureAwait(false);
        Task<TKetQua> viec;
        try
        {
            viec = Task.Run(async () =>
            {
                try
                {
                    huy.ThrowIfCancellationRequested();
                    // DO-NOT-CHANGE: Disposing on caller timeout can make the pending notification query throw ObjectDisposedException.
                    await using var phamVi = taoPhamVi.CreateAsyncScope();
                    var kho = phamVi.ServiceProvider.GetRequiredService<IThietBiNhanThongBaoRepository>();
                    return await thaoTac(kho, huy).ConfigureAwait(false);
                }
                finally { slot.Release(); }
            }, CancellationToken.None);
        }
        catch
        {
            slot.Release();
            throw;
        }
        return await viec.ConfigureAwait(false);
    }
}
