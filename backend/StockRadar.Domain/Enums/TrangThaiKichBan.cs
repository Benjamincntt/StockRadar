namespace StockRadar.Domain.Enums;

/// <summary>
/// Trạng thái vòng đời của một kịch bản giao dịch.
/// WATCHING → FORMING → TRIGGERED → HOLDING → TAKE_PROFIT / INVALIDATED / EXIT
/// </summary>
public enum TrangThaiKichBan
{
    /// <summary>Đang theo dõi — chưa đạt bối cảnh</summary>
    DangTheoDoi = 0,

    /// <summary>Đang hình thành — đạt bối cảnh + hình thái, chờ cò kích hoạt</summary>
    DangHinhThanh = 1,

    /// <summary>Đã kích hoạt — đủ cả 3 vai trò, sẵn sàng hành động</summary>
    DaKichHoat = 2,

    /// <summary>Đang giữ vị thế — đã vào lệnh</summary>
    DangGiu = 3,

    /// <summary>Chốt lời — đã đạt TP</summary>
    ChotLoi = 4,

    /// <summary>Hủy lệnh — chạm SL hoặc invalidation</summary>
    HuyLenh = 5,

    /// <summary>Thoát lệnh — sell scenario triggered (Kiệt sức / Gãy nền)</summary>
    ThoatLenh = 6
}
