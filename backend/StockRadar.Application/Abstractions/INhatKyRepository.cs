namespace StockRadar.Application.Abstractions;

/// <summary>Read model một lệnh nhật ký — Application không tham chiếu entity của Infrastructure.</summary>
public sealed record NhatKyLenhRow(
    Guid Id,
    Guid UserId,
    string MaChungKhoan,
    string Chieu,
    DateTime ThoiDiemKhop,
    decimal GiaKhop,
    decimal KhoiLuong,
    decimal? Phi,
    string? NguonTinHieu,
    string? MaTinHieu,
    decimal? GiaTinHieuChup,
    string? GhiChu,
    string? LyDo,
    decimal? KLieuChinh,
    decimal? VonDieuChinh,
    DateTime TaoNgay);

/// <summary>Read model đánh dấu bỏ qua tín hiệu (Q3).</summary>
public sealed record NhatKyBoQuaRow(
    Guid Id,
    Guid UserId,
    string Nguon,
    string MaTinHieu,
    string Symbol,
    string? LyDo,
    DateTime BoQuaNgay);

/// <summary>
/// Repository lệnh nhật ký giao dịch (NhatKyGiaoDich).
/// SECURITY: mọi method đều nhận userId và chỉ chạm dữ liệu của đúng user đó (N6/FR-009).
/// </summary>
public interface INhatKyRepository
{
    Task<(List<NhatKyLenhRow> Items, int Total)> LayDanhSachAsync(
        Guid userId, string? symbol, DateTime? tuUtc, DateTime? denUtc, string? loaiLenh,
        int page, int size, CancellationToken ct = default);

    Task<NhatKyLenhRow?> LayMotAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>Toàn bộ lệnh của user theo thời điểm tăng dần — dùng cho projection vị thế/thống kê.</summary>
    Task<List<NhatKyLenhRow>> LayToanBoLenhAsync(Guid userId, CancellationToken ct = default);

    Task ThemAsync(NhatKyLenhRow row, CancellationToken ct = default);

    Task SuaAsync(NhatKyLenhRow row, CancellationToken ct = default);

    Task XoaAsync(Guid userId, Guid id, CancellationToken ct = default);

    Task<NhatKyBoQuaRow?> LayBoQuaAsync(Guid userId, string nguon, string maTinHieu, CancellationToken ct = default);

    Task<List<NhatKyBoQuaRow>> LayBoQuaTheoNguonAsync(Guid userId, string nguon, CancellationToken ct = default);

    Task ThemBoQuaAsync(NhatKyBoQuaRow row, CancellationToken ct = default);

    Task XoaBoQuaAsync(Guid userId, string nguon, string maTinHieu, CancellationToken ct = default);
}
