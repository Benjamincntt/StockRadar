using StockRadar.Domain.Services;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Bộ xếp hạng cơ hội V2 — xếp hạng các kịch bản đã TRIGGERED.
/// 6 tiêu chí: RS(25%), Sector(20%), Chất lượng trigger(20%), Regime(15%), R:R(10%), Confluence(10%).
/// KHÔNG có quyền veto.
/// </summary>
public interface IXepHangCoHoi
{
    /// <summary>
    /// Xếp hạng danh sách kịch bản đã trigger, trả về Top N.
    /// </summary>
    Task<IReadOnlyList<KetQuaXepHang>> XepHangAsync(
        IReadOnlyList<KetQuaKichBan> daKichHoat,
        CancellationToken ct = default);
}
