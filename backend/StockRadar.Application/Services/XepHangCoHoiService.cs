using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Bộ xếp hạng cơ hội V2 — xếp hạng các kịch bản đã TRIGGERED.
/// 6 tiêu chí: RS(25%), Sector(20%), Chất lượng trigger(20%), Regime(15%), R:R(10%), Confluence(10%).
/// KHÔNG có quyền veto — chỉ xếp hạng ưu tiên, mã điểm thấp vẫn có thể lọt Top N.
/// </summary>
public class XepHangCoHoiService : IXepHangCoHoi
{
    private readonly XepHangOptions _options;
    private readonly INguonDuLieuXepHang _nguonDuLieu;
    private readonly ILogger<XepHangCoHoiService> _logger;

    public XepHangCoHoiService(
        IOptions<XepHangOptions> options,
        INguonDuLieuXepHang nguonDuLieu,
        ILogger<XepHangCoHoiService> logger)
    {
        _options = options.Value;
        _nguonDuLieu = nguonDuLieu;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KetQuaXepHang>> XepHangAsync(
        IReadOnlyList<KetQuaKichBan> daKichHoat,
        CancellationToken ct = default)
    {
        if (daKichHoat is null || daKichHoat.Count == 0)
            return Array.Empty<KetQuaXepHang>();

        // Pha thị trường là toàn cục — chỉ cần lấy một lần cho cả đợt xếp hạng.
        var phaThiTruong = await _nguonDuLieu.LayPhaThiTruongAsync(ct);
        var diemRegime = TinhDiemRegime(phaThiTruong);

        // Confluence: đếm số kịch bản TRIGGERED của cùng một symbol (không phân biệt loại).
        var soKichBanTheoSymbol = daKichHoat
            .GroupBy(k => k.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Cache dữ liệu theo symbol để tránh gọi lặp khi một mã có nhiều kịch bản.
        var cacheRs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var cacheNganh = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var ketQua = new List<KetQuaXepHang>(daKichHoat.Count);

        foreach (var kichBan in daKichHoat)
        {
            ct.ThrowIfCancellationRequested();

            if (!cacheRs.TryGetValue(kichBan.Symbol, out var rsPercentile))
            {
                var rs = await _nguonDuLieu.LayRsPercentileAsync(kichBan.Symbol, ct);
                rsPercentile = rs ?? 50m; // Không lấy được RS → mặc định trung tính 50.
                cacheRs[kichBan.Symbol] = rsPercentile;
            }

            if (!cacheNganh.TryGetValue(kichBan.Symbol, out var trangThaiNganh))
            {
                trangThaiNganh = await _nguonDuLieu.LayTrangThaiNganhAsync(kichBan.Symbol, ct);
                cacheNganh[kichBan.Symbol] = trangThaiNganh;
            }

            var diemRs = TinhDiemRs(rsPercentile);
            var diemSector = TinhDiemSector(trangThaiNganh);
            var diemTrigger = TinhDiemChatLuongTrigger(kichBan);
            var diemTyLeLaiLo = TinhDiemTyLeLaiLo(kichBan.KeHoach);
            var diemConfluence = TinhDiemConfluence(soKichBanTheoSymbol.GetValueOrDefault(kichBan.Symbol, 1));

            var diemTong =
                (diemRs * _options.TrongSoRs) +
                (diemSector * _options.TrongSoSector) +
                (diemTrigger * _options.TrongSoChatLuongTrigger) +
                (diemRegime * _options.TrongSoRegime) +
                (diemTyLeLaiLo * _options.TrongSoTyLeLaiLo) +
                (diemConfluence * _options.TrongSoConfluence);

            ketQua.Add(new KetQuaXepHang(
                kichBan,
                LamTron(diemTong),
                LamTron(diemRs),
                LamTron(diemSector),
                LamTron(diemTrigger),
                LamTron(diemRegime),
                LamTron(diemTyLeLaiLo),
                LamTron(diemConfluence)));
        }

        var top = ketQua
            .OrderByDescending(k => k.DiemTong)
            .Take(_options.SoLuongTop)
            .ToList();

        _logger.LogDebug(
            "Xếp hạng {Input} kịch bản TRIGGERED → trả về Top {Top} (điểm cao nhất {Max}).",
            daKichHoat.Count, top.Count, top.Count > 0 ? top[0].DiemTong : 0m);

        return top;
    }

    /// <summary>Tiêu chí 1 — RS (sức mạnh tương đối). Điểm = percentile (0-100).</summary>
    internal static decimal TinhDiemRs(decimal rsPercentile) => Clamp0_100(rsPercentile);

    /// <summary>
    /// Tiêu chí 2 — Sóng ngành. Active=100, Emerging=70, None=30, không xác định=50.
    /// </summary>
    internal static decimal TinhDiemSector(string? trangThaiNganh)
    {
        if (string.IsNullOrWhiteSpace(trangThaiNganh))
            return 50m;

        return trangThaiNganh.Trim().ToLowerInvariant() switch
        {
            "active" or "strong" => 100m,
            "emerging" => 70m,
            "none" => 30m,
            _ => 50m
        };
    }

    /// <summary>
    /// Tiêu chí 3 — Chất lượng trigger = TB(volumeScore, macdScore, rsiScore), tính từ ScenarioResult.
    /// </summary>
    internal static decimal TinhDiemChatLuongTrigger(KetQuaKichBan kichBan)
    {
        var volumeScore = TinhDiemVolume(kichBan);
        var macdScore = TinhDiemMacd(kichBan);
        var rsiScore = TinhDiemRsi(kichBan);
        return (volumeScore + macdScore + rsiScore) / 3m;
    }

    /// <summary>
    /// Điểm volume: normalize tỷ lệ volume → min(ratio / 3, 1) × 100.
    /// Ưu tiên <see cref="BangChupChiBao.VolumeRatio"/>; nếu không có thì parse từ bằng chứng; mặc định 0.
    /// </summary>
    private static decimal TinhDiemVolume(KetQuaKichBan kichBan)
    {
        decimal? ratio = kichBan.BangChup?.VolumeRatio;

        if (ratio is null)
        {
            // Fallback: đọc từ bằng chứng "volume thực tế / ngưỡng".
            var bangChung = kichBan.DanhSachBangChung
                .FirstOrDefault(b => b.MoTa.Contains("volume", StringComparison.OrdinalIgnoreCase)
                                     || b.MoTa.Contains("kl", StringComparison.OrdinalIgnoreCase));
            if (bangChung is not null)
            {
                var thucTe = ParseSo(bangChung.GiaTriThucTe);
                var nguong = ParseSo(bangChung.Nguong);
                if (thucTe is not null && nguong is > 0)
                    ratio = thucTe.Value / nguong.Value;
                else if (thucTe is not null)
                    ratio = thucTe.Value;
            }
        }

        var r = ratio ?? 0m;
        return Clamp0_100(Math.Min(r / 3m, 1m) * 100m);
    }

    /// <summary>
    /// Điểm MACD: parse từ bằng chứng nếu có; mặc định 50 (trung tính) khi không xác định được độ mở rộng.
    /// </summary>
    private static decimal TinhDiemMacd(KetQuaKichBan kichBan)
    {
        var bangChung = kichBan.DanhSachBangChung
            .FirstOrDefault(b => b.MoTa.Contains("macd", StringComparison.OrdinalIgnoreCase));

        if (bangChung is null)
            return 50m;

        var giaTri = ParseSo(bangChung.GiaTriThucTe);
        if (giaTri is null)
            return 50m;

        // Histogram dương và mở rộng → tốt; âm → yếu. Chuẩn hóa quanh mốc 50.
        return Clamp0_100(50m + (giaTri.Value > 0 ? 25m : giaTri.Value < 0 ? -25m : 0m));
    }

    /// <summary>
    /// Điểm RSI tại trigger, phân biệt kịch bản Breakout (mặc định) và Pullback (Hồi hỗ trợ).
    /// </summary>
    private static decimal TinhDiemRsi(KetQuaKichBan kichBan)
    {
        var bangChup = kichBan.BangChup;

        decimal rsi;
        if (bangChup is not null)
        {
            rsi = bangChup.Rsi;
        }
        else
        {
            var bangChung = kichBan.DanhSachBangChung
                .FirstOrDefault(b => b.MoTa.Contains("rsi", StringComparison.OrdinalIgnoreCase));
            var parsed = bangChung is null ? null : ParseSo(bangChung.GiaTriThucTe);
            if (parsed is null)
                return 50m; // Không có RSI → trung tính.
            rsi = parsed.Value;
        }

        var laPullback = kichBan.LoaiKichBan == LoaiKichBan.HoiHoTro;
        return laPullback ? DiemRsiPullback(rsi) : DiemRsiBreakout(rsi);
    }

    /// <summary>Breakout: 55-65=100, 50-55 hoặc 65-70=70, &gt;75=40, còn lại=50.</summary>
    private static decimal DiemRsiBreakout(decimal rsi)
    {
        if (rsi >= 55m && rsi <= 65m) return 100m;
        if ((rsi >= 50m && rsi < 55m) || (rsi > 65m && rsi <= 70m)) return 70m;
        if (rsi > 75m) return 40m;
        return 50m;
    }

    /// <summary>Pullback: 45-55=100, 40-45 hoặc 55-60=70, còn lại=50.</summary>
    private static decimal DiemRsiPullback(decimal rsi)
    {
        if (rsi >= 45m && rsi <= 55m) return 100m;
        if ((rsi >= 40m && rsi < 45m) || (rsi > 55m && rsi <= 60m)) return 70m;
        return 50m;
    }

    /// <summary>Tiêu chí 4 — Pha thị trường. Favorable=100, Neutral=60, Unfavorable=25, khác=60.</summary>
    internal static decimal TinhDiemRegime(string? phaThiTruong)
    {
        if (string.IsNullOrWhiteSpace(phaThiTruong))
            return 60m;

        return phaThiTruong.Trim().ToLowerInvariant() switch
        {
            "favorable" => 100m,
            "unfavorable" => 25m,
            _ => 60m
        };
    }

    /// <summary>Tiêu chí 5 — R:R. normalize → min(TyLeLaiLo / 3, 1) × 100.</summary>
    internal static decimal TinhDiemTyLeLaiLo(KeHoachGiaoDich? keHoach)
    {
        var tyLe = keHoach?.TyLeLaiLo ?? 0m;
        return Clamp0_100(Math.Min(tyLe / 3m, 1m) * 100m);
    }

    /// <summary>Tiêu chí 6 — Confluence. 1 kịch bản=50, 2=80, 3+=100.</summary>
    internal static decimal TinhDiemConfluence(int soKichBanCungSymbol) => soKichBanCungSymbol switch
    {
        <= 1 => 50m,
        2 => 80m,
        _ => 100m
    };

    /// <summary>Parse số thập phân đầu tiên trong chuỗi (hỗ trợ "2.1×", "1,5", "≥ 60").</summary>
    private static decimal? ParseSo(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = -1;
        var hasDot = false;
        for (var i = 0; i < text!.Length; i++)
        {
            var c = text[i];
            if (char.IsDigit(c))
            {
                if (start < 0) start = i;
            }
            else if ((c == '.' || c == ',') && start >= 0 && !hasDot && i + 1 < text.Length && char.IsDigit(text[i + 1]))
            {
                hasDot = true;
            }
            else if (start >= 0)
            {
                break;
            }
        }

        if (start < 0)
            return null;

        var end = start;
        var seenSeparator = false;
        while (end < text.Length && (char.IsDigit(text[end]) ||
               ((text[end] == '.' || text[end] == ',') && !seenSeparator)))
        {
            if (text[end] == '.' || text[end] == ',') seenSeparator = true;
            end++;
        }

        var raw = text.Substring(start, end - start).Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal Clamp0_100(decimal value) => Math.Clamp(value, 0m, 100m);

    /// <summary>Làm tròn 2 chữ thập phân để điểm số ổn định, dễ so sánh trong test.</summary>
    private static decimal LamTron(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
