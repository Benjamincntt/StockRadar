using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nguồn lịch chốt quyền toàn thị trường từ FireAnt (GET {ApiBaseUrl}/events/search, symbol trống).
/// FireAnt mở dữ liệu cho khách vãng lai: token "guest" nằm sẵn trong bundle Next.js của trang chủ,
/// nên ở đây lấy token bằng cách scrape bundle (cache 12h), rồi gọi events/search cho cửa sổ
/// [tuNgay-45, tuNgay+soNgay]. Sự kiện được giữ khi còn chặn (executionDate — hoặc recordDate
/// nếu nguồn không có executionDate — chưa qua). Kết quả (symbol → sự kiện chặn lâu nhất)
/// cache 6h. Mọi lỗi mạng/format → trả rỗng (fail-open) để không chặn oan cả bảng Top.
/// </summary>
internal sealed class FireAntLichChotQuyenClient(
    HttpClient http,
    IMemoryCache cache,
    IOptions<FireAntOptions> options,
    ILogger<FireAntLichChotQuyenClient> logger) : INguonLichChotQuyen
{
    private const string TokenCacheKey = "fireant:guest-token";
    private static readonly IReadOnlyDictionary<string, ThongTinChotQuyen> Trong =
        new Dictionary<string, ThongTinChotQuyen>(StringComparer.OrdinalIgnoreCase);

    // type 1 = tiền mặt, 2 = cổ phiếu, 3 = phát hành cho CĐHH (quyền mua) — đều là "chia chác".
    private static readonly HashSet<int> CacKieuChiaChac = new() { 1, 2, 3 };

    public async Task<IReadOnlyDictionary<string, ThongTinChotQuyen>> LayMaSapChotQuyenAsync(
        DateOnly tuNgay,
        int soNgay,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled || soNgay <= 0)
            return Trong;

        var ngay = Math.Min(soNgay, 120);
        var cacheKey = $"fireant:exdates:{tuNgay:yyyyMMdd}:{ngay}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, ThongTinChotQuyen>? cached) && cached is not null)
            return cached;

        try
        {
            var token = await LayTokenAsync(cfg, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("FireAnt: không lấy được guest token — cổng chia chác mở (fail-open)");
                return Trong;
            }

            var den = tuNgay.AddDays(ngay);
            // Lấy lùi về quá khứ 45 ngày: sự kiện đã chốt quyền nhưng CHƯA thực hiện
            // (executionDate tương lai) vẫn phải bị chặn — khoảng [chốt → thực hiện] có thể
            // kéo dài vài tuần (cổ tức trả sau, quyền mua đăng ký).
            var tuTim = tuNgay.AddDays(-45);
            var bd = tuTim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var ed = den.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url =
                $"{cfg.ApiBaseUrl.TrimEnd('/')}/events/search?symbol=&orderBy=1&type=0"
                + $"&startDate={bd}&endDate={ed}&offset=0&limit=1000";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("FireAnt events/search HTTP {Status} — cổng chia chác mở", response.StatusCode);
                return Trong;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var map = new Dictionary<string, ThongTinChotQuyen>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var ev in doc.RootElement.EnumerateArray())
                {
                    var symbol = LayChu(ev, "symbol");
                    var typeEl = ev.TryGetProperty("type", out var t) ? t : default;
                    var kieu = typeEl.ValueKind == JsonValueKind.Number ? typeEl.GetInt32() : -1;
                    if (string.IsNullOrWhiteSpace(symbol) || !CacKieuChiaChac.Contains(kieu))
                        continue;
                    if (!LayNgay(ev, "recordDate", out var recordDate))
                        continue;
                    if (recordDate > den)
                        continue;
                    // Ngày hết chặn = ngày thực hiện quyền (nếu nguồn có), không thì = ngày chốt.
                    DateOnly? thucHien = LayNgay(ev, "executionDate", out var thucHienNgay)
                        ? thucHienNgay
                        : null;
                    if ((thucHien ?? recordDate) < tuNgay)
                        continue; // sự kiện đã chốt lẫn đã thực hiện xong → mã tự do

                    // Title FireAnt mô tả đúng "chia gì, bao nhiêu" (vd "Cổ tức đợt 1/2026 bằng
                    // tiền, tỷ lệ 1.000đ/CP") → đưa vào nhãn chặn để người dùng hiểu vì sao bị loại.
                    var moTa = LayChu(ev, "title");
                    var ma = symbol.Trim().ToUpperInvariant();
                    var moi = new ThongTinChotQuyen(recordDate, moTa, thucHien, DaChot: recordDate < tuNgay);
                    if (!map.TryGetValue(ma, out var hienTai))
                    {
                        map[ma] = moi;
                    }
                    else
                    {
                        var hetHienTai = hienTai.NgayThucHien ?? hienTai.ExDate;
                        var hetMoi = thucHien ?? recordDate;
                        if (hetMoi > hetHienTai)
                        {
                            // Sự kiện kéo dài hơn → chiếm chỗ (chặn tới ngày xa nhất).
                            map[ma] = moi;
                        }
                        else if (hetMoi == hetHienTai && !string.IsNullOrWhiteSpace(moTa))
                        {
                            // Cùng hạn nhưng khác sự kiện (vd vừa trả tiền vừa phát hành) → gộp mô tả.
                            if (string.IsNullOrWhiteSpace(hienTai.MoTa))
                                map[ma] = hienTai with { MoTa = moTa };
                            else if (!hienTai.MoTa.Contains(moTa!, StringComparison.OrdinalIgnoreCase))
                                map[ma] = hienTai with { MoTa = $"{hienTai.MoTa} · {moTa}" };
                        }
                    }
                }
            }

            cache.Set(cacheKey, (IReadOnlyDictionary<string, ThongTinChotQuyen>)map, TimeSpan.FromHours(6));
            logger.LogInformation("FireAnt: {Count} mã sắp chốt quyền trong {Ngay} ngày tới", map.Count, ngay);
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "FireAnt events/search lỗi — cổng chia chác mở (fail-open)");
            return Trong;
        }
    }

    private async Task<string?> LayTokenAsync(FireAntOptions cfg, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cfg.AccessToken))
            return cfg.AccessToken;

        if (cache.TryGetValue(TokenCacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        try
        {
            var html = await http.GetStringAsync(cfg.SiteUrl.TrimEnd('/') + "/", cancellationToken);
            var chunkMatch = Regex.Match(html, "/_next/static/chunks/pages/_app-[A-Za-z0-9]+\\.js");
            if (!chunkMatch.Success)
            {
                logger.LogWarning("FireAnt: không tìm thấy _app chunk trong trang chủ");
                return null;
            }

            var chunk = await http.GetStringAsync(cfg.SiteUrl.TrimEnd('/') + chunkMatch.Value, cancellationToken);
            var tokenMatch = Regex.Match(chunk, "ANONYMOUS_ACCESS_TOKEN\\s*[:=]\\s*\"([A-Za-z0-9._\\-]{20,})\"");
            if (!tokenMatch.Success)
            {
                logger.LogWarning("FireAnt: không trích được ANONYMOUS_ACCESS_TOKEN từ bundle");
                return null;
            }

            var token = tokenMatch.Groups[1].Value;
            cache.Set(TokenCacheKey, token, TimeSpan.FromHours(12));
            return token;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "FireAnt: scrape token thất bại");
            return null;
        }
    }

    private static string? LayChu(JsonElement el, string ten) =>
        el.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool LayNgay(JsonElement el, string ten, out DateOnly ngay)
    {
        ngay = default;
        if (!el.TryGetProperty(ten, out var v) || v.ValueKind != JsonValueKind.String)
            return false;
        var s = v.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return false;
        // FireAnt trả "2026-10-05T00:00:00" — cắt phần ngày.
        var phanNgay = s.Length >= 10 ? s.Substring(0, 10) : s;
        return DateOnly.TryParseExact(phanNgay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out ngay);
    }
}
