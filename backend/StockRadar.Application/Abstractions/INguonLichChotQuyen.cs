namespace StockRadar.Application.Abstractions;

/// <summary>
/// Nguồn lịch sự kiện quyền (ngày không hưởng quyền / chốt quyền chia chác) TOÀN THỊ TRƯỜNG
/// cho cửa sổ ngày sắp tới. Khác <see cref="StockRadar.Domain.Services.INguonSuKienQuyen"/>
/// (file tay, chỉ dùng để điều chỉnh nến quá khứ) — đây là feed tự động phục vụ cổng lọc
/// "loại mã sắp chia". Trả về rỗng khi nguồn không khả dụng (fail-open, không chặn oan).
/// </summary>
public interface INguonLichChotQuyen
{
    /// <summary>symbol (UPPERCASE) → ngày ex-date gần nhất trong [tuNgay, tuNgay+soNgay].</summary>
    Task<IReadOnlyDictionary<string, DateOnly>> LayMaSapChotQuyenAsync(
        DateOnly tuNgay,
        int soNgay,
        CancellationToken cancellationToken = default);
}
