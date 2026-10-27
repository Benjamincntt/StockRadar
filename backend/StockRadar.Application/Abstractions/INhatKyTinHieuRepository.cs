namespace StockRadar.Application.Abstractions;

/// <summary>
/// Read model tín hiệu V3 (dòng <c>TinHieuV3</c> đã vào lệnh) cho picker nhật ký — chỉ đọc, không đổi pipeline.
/// Giá chụp theo chiều lệnh V3: mua → <see cref="GiaVao"/>; bán → <see cref="GiaThoat"/> (FR-040).
/// </summary>
public sealed record TinHieuV3Row(
    long Id,
    string Symbol,
    string LoaiTinHieu,
    DateOnly NgayTinHieu,
    decimal? GiaVao,
    decimal? GiaThoat,
    string? KetQua);

/// <summary>Kết quả tín hiệu V3 đã đóng (song song lãi/lỗ thật của lệnh): bucket + % lợi nhuận.</summary>
public sealed record KetQuaV3Row(long Id, string? KetQua, decimal? PhanTramLoiNhuan);

/// <summary>
/// Nguồn tín hiệu cho picker nhật ký (FR-040).
/// LIMIT: chỉ SELECT từ <c>TinHieuV3</c> — cấm ghi, không còn đọc bảng kịch bản V2 (đã gỡ).
/// </summary>
public interface INhatKyTinHieuRepository
{
    Task<(List<TinHieuV3Row> Items, int Total)> LayTinHieuV3Async(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default);

    Task<TinHieuV3Row?> LayMotTinHieuV3Async(long id, CancellationToken ct = default);

    /// <summary>Tra nhanh kết quả theo danh sách id (không lọc trạng thái — tín hiệu đã chuyển vòng đời vẫn có kết quả).</summary>
    Task<List<KetQuaV3Row>> LayKetQuaV3Async(IReadOnlyList<long> ids, CancellationToken ct = default);
}
