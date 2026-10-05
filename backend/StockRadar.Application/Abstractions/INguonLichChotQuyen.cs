using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Nguồn lịch sự kiện quyền (ngày không hưởng quyền / chốt quyền chia chác) TOÀN THỊ TRƯỜNG
/// cho cửa sổ ngày sắp tới. Khác <see cref="StockRadar.Domain.Services.INguonSuKienQuyen"/>
/// (file tay, chỉ dùng để điều chỉnh nến quá khứ) — đây là feed tự động phục vụ cổng lọc
/// "loại mã sắp chia". Trả về rỗng khi nguồn không khả dụng (fail-open, không chặn oan).
/// </summary>
public interface INguonLichChotQuyen
{
    /// <summary>symbol (UPPERCASE) → sự kiện chốt quyền gần nhất trong [tuNgay, tuNgay+soNgay],
    /// kèm mô tả chia gì/tỷ lệ bao nhiêu để hiển thị trên nhãn chặn.</summary>
    Task<IReadOnlyDictionary<string, ThongTinChotQuyen>> LayMaSapChotQuyenAsync(
        DateOnly tuNgay,
        int soNgay,
        CancellationToken cancellationToken = default);
}
