using StockRadar.Domain.Enums;

namespace StockRadar.Application.Abstractions;

/// <summary>Buffered delivery after persistence; see specs/013-mobile-push-notify/plan.md for independent ordered channels.</summary>
public interface IGuiThongBao
{
    /// <summary>Buffers a V3 message without sending; flush only after the tick's state is saved.</summary>
    /// <param name="loai">V3 notification type.</param>
    /// <param name="symbol">Stock symbol carried in push navigation data.</param>
    /// <param name="kichBan">Scenario used in the push title.</param>
    /// <param name="telegramHtml">Original, unmodified Telegram HTML.</param>
    void DatTinV3(LoaiTinV3 loai, string symbol, LoaiKichBanV3 kichBan, string telegramHtml);

    /// <summary>Buffers a plain-text system message without sending it.</summary>
    /// <param name="tieuDeDay">Push notification title.</param>
    /// <param name="noiDung">Original body shared by both channels.</param>
    void DatTinHeThong(string tieuDeDay, string noiDung);

    /// <summary>Drains the buffer through two independent loops, preserving order within each channel.</summary>
    /// <param name="cancellationToken">Tick cancellation; discards remaining messages without throwing.</param>
    /// <returns>Completion after delivery attempts or cancellation; delivery failures do not escape.</returns>
    Task GuiTatCaAsync(CancellationToken cancellationToken = default);

    /// <summary>Buffers and flushes a system message when no tick persistence must precede delivery.</summary>
    /// <param name="tieuDeDay">Push notification title.</param>
    /// <param name="noiDung">Original plain-text Telegram body.</param>
    /// <param name="cancellationToken">Caller cancellation; handled without throwing.</param>
    /// <returns>Completion of the buffered delivery attempts.</returns>
    Task GuiHeThongAsync(string tieuDeDay, string noiDung, CancellationToken cancellationToken = default);
}

public sealed record TinDaGom(LoaiTinV3? LoaiV3, string? Symbol, LoaiKichBanV3? KichBan,
    string? TieuDeHeThong, string NoiDungTelegram);
