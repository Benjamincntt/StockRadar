using StockRadar.Domain.Enums;

namespace StockRadar.Application.Abstractions;

/// <summary>Push delivery contract; see specs/013-mobile-push-notify/plan.md for routing and token retention rules.</summary>
public interface INguiTinDay
{
    bool DangHoatDong { get; }

    /// <summary>Sends per-device push messages; only unregistered tokens are returned for cleanup.</summary>
    /// <param name="tokens">Device registrations to attempt once each.</param>
    /// <param name="tin">Notification content and navigation metadata.</param>
    /// <param name="cancellationToken">Channel timeout or caller cancellation.</param>
    /// <returns>Successful sends and invalid tokens; disabled channels return an empty result.</returns>
    /// <exception cref="OperationCanceledException">The supplied cancellation token was canceled.</exception>
    /// <exception cref="Exception">Transport failures propagate to the dispatcher's batch circuit.</exception>
    Task<DayTinKetQua> GuiAsync(IReadOnlyList<string> tokens, DayTinNhan tin,
        CancellationToken cancellationToken = default);
}

public sealed record DayTinNhan(string TieuDe, string NoiDung, string Kenh, string? Symbol,
    LoaiTinV3? LoaiV3 = null);

public sealed record DayTinKetQua(int SoDaGui, IReadOnlyList<string> TokenHong);
