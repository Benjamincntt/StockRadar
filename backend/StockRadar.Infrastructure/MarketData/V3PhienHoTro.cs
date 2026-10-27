using StockRadar.Domain.Entities;
using StockRadar.Domain.MarketData;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Tiện ích chung cho 4 runner V3: mốc thời gian T+2.5, phát hiện giá trần/sàn trắng
/// từ bảng giá realtime. Tập trung một chỗ để tránh lệch định nghĩa giữa các nhịp.
/// </summary>
internal static class V3PhienHoTro
{
    /// <summary>
    /// Đã tới cửa bán T+2.5 chưa (FR-017): tính từ phiên khớp mua. <paramref name="gioXet"/> là giờ VN
    /// hiện tại — ĐÚNG phiên T+2 chỉ bán từ đầu buổi chiều (cfg.MocPhieuChieuTplus2, 13:00),
    /// từ phiên T+3 trở đi bán cả ngày. Luật suy ra <see cref="V3LichPhien.DaToiTplus25"/>.
    /// </summary>
    public static bool DaQuaTplus25(DateOnly? ngayVao, DateOnly homNay, TimeOnly gioXet, V3CauHinh cfg)
    {
        if (ngayVao is null)
            return false;
        var soPhienGiua = TradingSessionMath.TradingSessionsBetween(ngayVao.Value, homNay);
        return V3LichPhien.DaToiTplus25(soPhienGiua, gioXet, cfg.Tplus25Sessions, cfg.MocPhieuChieuTplus2);
    }

    /// <summary>Số phiên đã giữ tính từ phiên tín hiệu tới hôm nay (FR-016).</summary>
    public static int SoPhienDaGi(DateOnly ngayTinHieu, DateOnly homNay) =>
        TradingSessionMath.TradingSessionsBetween(ngayTinHieu, homNay);

    /// <summary>Giá tham chiếu hôm nay = đóng cửa phiên liền trước (từ history hoàn thành tới hôm qua).</summary>
    public static decimal GiaThamChieu(Stock s, DateOnly homNay)
    {
        decimal prev = 0m;
        foreach (var b in s.History.OrderBy(x => x.Date))
        {
            if (b.Date < homNay)
                prev = b.Close;
            else
                break;
        }
        return prev;
    }

    /// <summary>
    /// Giá khớp rơi đúng/tại giá trần (FR-014) → hủy lệnh mua. Dung sai nửa bước giá (nghìn đồng).
    /// </summary>
    public static bool LaGiaTran(decimal giaKhop, decimal refPrice, string? exchange)
    {
        if (refPrice <= 0m || giaKhop <= 0m)
            return false;
        var band = ExchangePriceBand.BandFor(exchange);
        var ceiling = refPrice * (1m + band);
        var tol = Math.Max(ceiling * 0.002m, 0.05m);
        return giaKhop >= ceiling - tol;
    }

    /// <summary>
    /// Phiên "sàn trắng bên mua" (FR-018): hôm nay bị chất sàn, không người mua → CẤM bán.
    /// Nhận diện: biên độ trong phiên bằng 0 (High≈Low) và giá ở sát giá sàn.
    /// </summary>
    public static bool LaSanTrang(decimal giaKhop, decimal high, decimal low, decimal refPrice, string? exchange)
    {
        if (refPrice <= 0m || giaKhop <= 0m)
            return false;
        var band = ExchangePriceBand.BandFor(exchange);
        var floor = refPrice * (1m - band);
        var tol = Math.Max(floor * 0.005m, 0.1m);
        var namOTaiSan = Math.Abs(giaKhop - floor) <= tol;
        var khongBienDong = high <= 0m || low <= 0m || Math.Abs(high - low) <= tol;
        return namOTaiSan && khongBienDong;
    }
}
