using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Máy nhận kịch bản — interface chính của Scenario Engine V2.
/// Đánh giá bối cảnh + hình thái + cò kích hoạt cho từng mã × từng kịch bản.
/// </summary>
public interface IMayNhanKichBan
{
    /// <summary>
    /// Đánh giá tất cả kịch bản BUY cho một mã (Pha 1 — trước phiên).
    /// Trả về danh sách kết quả (một mã có thể có nhiều kịch bản ở các trạng thái khác nhau).
    /// </summary>
    Task<IReadOnlyList<KetQuaKichBan>> DanhGiaTruocPhienAsync(
        string symbol,
        IReadOnlyList<OhlcvBar> history,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra cò kích hoạt cho các mã đang FORMING (Pha 2 — trong phiên).
    /// </summary>
    Task<IReadOnlyList<KetQuaKichBan>> KiemTraTriggerTrongPhienAsync(
        IReadOnlyList<KetQuaKichBan> dangHinhThanh,
        IReadOnlyDictionary<string, decimal> giaHienTai,
        IReadOnlyDictionary<string, long> volumeHienTai,
        CancellationToken ct = default);

    /// <summary>
    /// Đánh giá kịch bản SELL cho vị thế đang giữ (Pha 2 — trong phiên).
    /// </summary>
    Task<IReadOnlyList<KetQuaKichBan>> DanhGiaBanAsync(
        string symbol,
        IReadOnlyList<OhlcvBar> history,
        decimal giaVaoLenh,
        CancellationToken ct = default);
}
