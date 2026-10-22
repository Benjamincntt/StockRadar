namespace StockRadar.Application.Abstractions;

/// <summary>Read model tín hiệu V2 (dòng KetQuaKichBan đã kích hoạt) cho picker — chỉ đọc, không đổi pipeline.</summary>
public sealed record TinHieuV2Row(
    long Id,
    string Symbol,
    string LoaiKichBan,
    DateTime NgayDanhGia,
    DateTime? ThoiGianKichHoat,
    decimal? GiaVaoThucTe,
    decimal? GiaMuaDiem2ThucTe,
    decimal? GiaBanNua,
    decimal? GiaThoatHet,
    string? KetQuaDoLuong,
    string? KeHoachJson);

/// <summary>Read model tín hiệu V1 VIP (vị thế MasterAlertPositions) cho picker — Q2, chỉ đọc.
/// N8: giá bán lấy từ nhịp bán PositionSellLegs; bảng vị thế không lưu giá mua điểm 2.</summary>
public sealed record TinHieuVipRow(
    Guid Id,
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    bool IsClosed,
    decimal? RealizedReturnOnDeployedPercent,
    decimal? GiaBanNua = null,
    decimal? GiaBanHet = null);

/// <summary>US3 — kết quả tín hiệu V2 đã đóng (song song với lãi/lỗ thật của lệnh): bucket đo lường + % lợi nhuận.</summary>
public sealed record KetQuaV2Row(long Id, string? KetQuaDoLuong, decimal? PhanTramLoiNhuan);

/// <summary>US3 — kết quả vị thế VIP: chỉ có realized % (V1 không lưu bucket đo lường).</summary>
public sealed record KetQuaVipRow(Guid Id, decimal? RealizedReturnOnDeployedPercent);

/// <summary>
/// nguồn tín hiệu cho picker nhật ký (N1/Q2).
/// LIMIT: chỉ SELECT từ KetQuaKichBan/MasterAlertPositions/PositionSellLegs — cấm ghi các bảng này (FR-007).
/// </summary>
public interface INhatKyTinHieuRepository
{
    Task<(List<TinHieuV2Row> Items, int Total)> LayTinHieuV2Async(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default);

    Task<TinHieuV2Row?> LayMotTinHieuV2Async(long id, CancellationToken ct = default);

    Task<(List<TinHieuVipRow> Items, int Total)> LayTinHieuVipAsync(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default);

    Task<TinHieuVipRow?> LayMotTinHieuVipAsync(Guid id, CancellationToken ct = default);

    /// <summary>US3: tra nhanh kết quả theo danh sách id (không lọc trạng thái — tín hiệu đã chuyển vòng đời vẫn phải có kết quả).</summary>
    Task<List<KetQuaV2Row>> LayKetQuaV2Async(IReadOnlyList<long> ids, CancellationToken ct = default);

    Task<List<KetQuaVipRow>> LayKetQuaVipAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
