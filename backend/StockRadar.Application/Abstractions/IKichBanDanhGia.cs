using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Interface đánh giá một loại kịch bản cụ thể.
/// Mỗi kịch bản (Nổ hướng lên, Hồi hỗ trợ, ...) implement interface này.
/// </summary>
public interface IKichBanDanhGia
{
    /// <summary>Loại kịch bản mà evaluator này xử lý</summary>
    LoaiKichBan LoaiKichBan { get; }

    /// <summary>Kịch bản này thuộc Buy side hay Sell side?</summary>
    bool LaKichBanBan { get; }

    /// <summary>
    /// Đánh giá bối cảnh — "Mã này có đáng để ý không?"
    /// </summary>
    KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history);

    /// <summary>
    /// Đánh giá hình thái — "Đang có setup đẹp không?"
    /// </summary>
    KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history);

    /// <summary>
    /// Kiểm tra cò kích hoạt — "BÂY GIỜ có phải lúc hành động?"
    /// </summary>
    KetQuaVaiTro KiemTraCoKichHoat(IReadOnlyList<OhlcvBar> history, decimal giaHienTai, long volumeHienTai);

    /// <summary>
    /// Tính kế hoạch giao dịch (Entry/SL/TP1/TP2) — chỉ gọi khi đã TRIGGERED.
    /// </summary>
    KeHoachGiaoDich TinhKeHoach(IReadOnlyList<OhlcvBar> history, decimal giaHienTai);
}
