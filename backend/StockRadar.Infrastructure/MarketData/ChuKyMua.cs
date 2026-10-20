using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Truy vấn chung xác định mã đang có chu kỳ MUA (C-0 / C-6 / C-7).
/// Dùng cho Pha 1 (chặn tạo FORMING), Pha 2 (chặn kích hoạt) và Phase 9 (dọn kết quả cũ).
/// Một dòng = một chu kỳ; "đóng" = ThoiGianThoatHet != null (C-19).
/// </summary>
internal static class ChuKyMua
{
    // DO-NOT-CHANGE: "đang mở" = ThoiGianKichHoat != null && ThoiGianThoatHet == null trên kịch bản MUA.
    // Lọc theo TrangThai sẽ coi vị thế còn giữ nhưng đã bị Pha 3 chấm/Hủy (NTP 2114) là đóng → cho mua lặp.
    private static IQueryable<KetQuaKichBanEntity> NguonKichBanMua(ApplicationDbContext db, IReadOnlyCollection<string> symbols) =>
        db.KetQuaKichBan.Where(e => symbols.Contains(e.Symbol)
            && e.ThoiGianKichHoat != null
            && e.LoaiKichBan != LoaiKichBan.KietSuc
            && e.LoaiKichBan != LoaiKichBan.GayNen);

    /// <summary>Mã còn chu kỳ MỞ (ThoiGianThoatHet == null). Pha 1 dùng để chặn tạo dòng FORMING mới.</summary>
    public static async Task<HashSet<string>> LayMaDangMoAsync(
        ApplicationDbContext db, IReadOnlyCollection<string> symbols, CancellationToken ct)
    {
        var danhSach = await NguonKichBanMua(db, symbols)
            .Where(e => e.ThoiGianThoatHet == null)
            .Select(e => e.Symbol)
            .ToListAsync(ct);
        return new HashSet<string>(danhSach, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mã bị chặn mua trong phiên hôm nay: đang mở HOẶC vừa đóng hôm nay.
    /// C-7: chu kỳ đóng rồi phải chờ tới PHIÊN SAU mới mua lại — đóng trong phiên hôm nay vẫn chặn hôm nay.
    /// </summary>
    public static async Task<HashSet<string>> LayMaBiChanMuaTrongPhienAsync(
        ApplicationDbContext db, IReadOnlyCollection<string> symbols, DateTime batDauHomNayUtc, CancellationToken ct)
    {
        var danhSach = await NguonKichBanMua(db, symbols)
            .Where(e => e.ThoiGianThoatHet == null || e.ThoiGianThoatHet >= batDauHomNayUtc)
            .Select(e => e.Symbol)
            .ToListAsync(ct);
        return new HashSet<string>(danhSach, StringComparer.OrdinalIgnoreCase);
    }
}
