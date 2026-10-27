using StockRadar.Domain.Entities;
using StockRadar.Domain.MarketData;

namespace StockRadar.Domain.Services;

/// <summary>
/// Máy tính chỉ báo toàn thị trường V3 (FR-001). Nhận danh sách mã với nến NGÀY ĐÃ CHUẨN HÓA
/// (nến giữ chỗ / dở dang đã được runner loại hoặc ghép giá realtime — xem V3CotNen),
/// trả về chỉ báo từng mã + chỉ báo diện thị trường tại phiên tín hiệu.
/// Tất cả định nghĩa theo research 01-phuong-phap.md; khác IndicatorMath nên tự tính.
/// </summary>
public static class V3ChiSoToanThiTruong
{
    /// <summary>Chỉ báo một mã trong phiên tín hiệu.</summary>
    public sealed record ChiTieuMa(
        string Symbol,
        string Sector,
        bool ThuocUni,
        decimal Tv20Ty,
        decimal Close,
        decimal Rsi14,
        decimal Ret20,
        /// <summary>SECR20 = trung vị RET20 của các mã UNI cùng ngành. null = mã chưa có ngành → KB4/N1 loại (FR-003).</summary>
        decimal? Secr20,
        decimal Rs63,
        decimal Rs126,
        int Floor20,
        bool LaPhienSan);

    /// <summary>Snapshot toàn thị trường tại phiên tín hiệu.</summary>
    public sealed record KetQua(
        DateOnly NgayTinHieu,
        int SoMaUni,
        decimal Breadth50,
        decimal ChiSo,
        decimal ChiSoMa50,
        /// <summary>(ChiSo / MA50 − 1) × 100; âm = chỉ số dưới MA50.</summary>
        decimal ChiSoDoLechMa50PhanTram,
        /// <summary>
        /// Lợi nhuận TB của PHIÊN TÍN HIỆU so với phiên liền trước (đã clip ±16%, chưa nhân cấp số).
        /// Nhịp nhẹ (08:30/14:25) không dựng lại cả chuỗi: lấy chuỗi đã lưu rồi cộng điểm hôm nay
        /// = mức cuối chuỗi × (1 + giá trị này).
        /// </summary>
        decimal ChiSoLoiNhuanHomNay,
        /// <summary>Các mã thuộc UNI tại phiên tín hiệu (TV20 ≥ ngưỡng, close ≥ ngưỡng) — danh sách nạp cho phiên kế tiếp.</summary>
        IReadOnlyList<string> DanhSachUni,
        int Left,
        int Tdm,
        IReadOnlyDictionary<string, ChiTieuMa> TheoMa);

    /// <summary>
    /// Một điểm của chuỗi chỉ số theo phiên (bảng <c>ChiSoNgayV3</c>) — nạp một lần khi deploy
    /// và nhịp 16:00 ghi lại cho phiên vừa đóng.
    /// </summary>
    public sealed record DiemChuoi(
        DateOnly Ngay,
        decimal ChiSo,
        decimal ChiSoMa50,
        decimal ChiSoDoLechMa50PhanTram,
        decimal LoiNhuan,
        int SoMaUni,
        decimal Breadth50,
        IReadOnlyList<string> DanhSachUni);

    /// <summary>
    /// Tính toàn bộ chỉ báo. inputs = mỗi mã kèm Bars là nến ngày đã chuẩn hóa, nến cuối = phiên tín hiệu T.
    /// </summary>
    public static KetQua Tinh(IReadOnlyList<Stock> tatCaMa, V3CauHinh cfg)
    {
        // 1. Tách close/volume/ngành/sàn cho từng mã; loại mã quá ngắn để có bất kỳ chỉ báo nào.
        var duLieu = new List<MaDuLieu>(tatCaMa.Count);
        foreach (var s in tatCaMa)
        {
            if (s.History is null || s.History.Count < 2)
                continue;
            var bars = s.History.OrderBy(b => b.Date).ToList();
            duLieu.Add(new MaDuLieu(s.Symbol, s.Sector, s.Exchange, bars));
        }

        DateOnly ngayTinHieu = duLieu.Count == 0
            ? default
            : duLieu.Max(d => d.Bars[^1].Date);

        // 2. Chỉ báo per-code (chưa có SECR20 / RS percentile — cần tập UNI).
        var soBo = duLieu.ToDictionary(d => d.Symbol, d => d);
        var tv20 = new Dictionary<string, decimal>();
        var ret20 = new Dictionary<string, decimal>();
        var rsi14 = new Dictionary<string, decimal>();
        var rs63 = new Dictionary<string, decimal>();
        var rs126 = new Dictionary<string, decimal>();
        var floor20 = new Dictionary<string, int>();
        var laSan = new Dictionary<string, bool>();
        var close = new Dictionary<string, decimal>();
        var isUni = new Dictionary<string, bool>();

        foreach (var d in duLieu)
        {
            var closes = d.Bars.Select(b => b.Close).ToList();
            var c = closes[^1];
            close[d.Symbol] = c;
            tv20[d.Symbol] = TinhTv20(d.Bars);
            ret20[d.Symbol] = TinhPhanTramLui(closes, 20);
            rsi14[d.Symbol] = RsiWilder(closes, 14);
            rs63[d.Symbol] = TinhPhanTramLoiNhuan(closes, 63);
            rs126[d.Symbol] = TinhPhanTramLoiNhuan(closes, 126);
            var bienDo = ExchangePriceBand.BandFor(d.Exchange);
            floor20[d.Symbol] = DemPhienSan(closes, bienDo, 20);
            laSan[d.Symbol] = LaPhienSan(closes, bienDo);

            // UNI: TV20 ≥ ngưỡng và close ≥ ngưỡng.
            isUni[d.Symbol] = tv20[d.Symbol] >= cfg.Tv20MinUni && c >= cfg.CloseMinUni;
        }

        var uni = duLieu.Where(d => isUni[d.Symbol]).Select(d => d.Symbol).ToHashSet();

        // 3. SECR20 = trung vị RET20 của các mã UNI cùng ngành.
        var theoNganh = duLieu
            .Where(d => uni.Contains(d.Symbol) && !string.IsNullOrWhiteSpace(d.Sector))
            .GroupBy(d => d.Sector!)
            .ToDictionary(
                g => g.Key,
                g => TrungVi(g.Select(x => ret20[x.Symbol]).ToList()));

        // 4. RS percentile (hạng phần trăm trong UNI) của rs63/rs126.
        var rs63Pct = HangPhanTramTrongUni(uni, rs63);
        var rs126Pct = HangPhanTramTrongUni(uni, rs126);

        // 5. Trục phiên chung: lợi nhuận TB theo UNI.shift(), UNI và BREADTH50 của TỪNG phiên.
        var diem = TinhDiemTheoTruc(duLieu, uni, cfg);
        var diemCuoi = diem.Count > 0 ? diem[^1] : null;
        var danhSachUni = diemCuoi?.DanhSachUni ?? System.Array.Empty<string>();
        decimal breadth = diemCuoi?.Breadth50 ?? 0m;

        // 6. Chỉ số bình quân đều (MKT) + MA50 trên chính trục đó.
        var (chiSo, chiSoMa50, doLech, loiNhuanHomNay) = TinhMucCuoi(diem);

        // 7. LEFT / TDM theo lịch phiên thật của tháng chứa ngày tín hiệu.
        var (left, tdm) = V3LichPhien.LeftVaNgayTrongThang(ngayTinHieu);

        var theoMa = new Dictionary<string, ChiTieuMa>(soBo.Count);
        foreach (var d in duLieu)
        {
            // BUSINESS-RULE: SECR20 chỉ lấy từ trung vị ngành (FR-003). Mã không có ngành
            // (hoặc ngành chưa có mã nào trong UNI) → SECR20 = null, KHÔNG thay bằng RET20 của chính nó.
            decimal? secr = d.Sector is { Length: > 0 } sec && theoNganh.TryGetValue(sec, out var v) ? v : null;
            theoMa[d.Symbol] = new ChiTieuMa(
                d.Symbol,
                d.Sector ?? string.Empty,
                uni.Contains(d.Symbol),
                tv20[d.Symbol],
                close[d.Symbol],
                rsi14[d.Symbol],
                ret20[d.Symbol],
                secr,
                rs63Pct.GetValueOrDefault(d.Symbol, 50m),
                rs126Pct.GetValueOrDefault(d.Symbol, 50m),
                floor20[d.Symbol],
                laSan[d.Symbol]);
        }

        return new KetQua(
            ngayTinHieu,
            uni.Count,
            breadth,
            chiSo,
            chiSoMa50,
            doLech,
            loiNhuanHomNay,
            danhSachUni,
            left,
            tdm,
            theoMa);
    }

    /// <summary>
    /// Chuỗi chỉ số + UNI theo TỪNG phiên cho toàn bộ lịch sử đã nạp — dùng cho nhịp 16:00 ghi
    /// <c>ChiSoNgayV3</c> và cho lần nạp một lần khi deploy (từ 01/2023). Cùng định nghĩa với
    /// <see cref="Tinh"/> (UNI.shift(), clip ±16%, MA50 cửa sổ min(50, số phiên)) — không có luật riêng.
    /// Trả rỗng khi lịch sử quá ngắn. KHÔNG tạo tín hiệu hay vị thế.
    /// </summary>
    public static IReadOnlyList<DiemChuoi> TinhChuoi(
        IReadOnlyList<Stock> tatCaMa, V3CauHinh cfg, DateOnly? tuNgay = null)
    {
        var duLieu = new List<MaDuLieu>(tatCaMa.Count);
        foreach (var s in tatCaMa)
        {
            if (s.History is null || s.History.Count < 2)
                continue;
            duLieu.Add(new MaDuLieu(s.Symbol, s.Sector, s.Exchange, s.History.OrderBy(b => b.Date).ToList()));
        }

        // Mã làm trục = UNI tại phiên cuối (đúng như Tinh) — các mã khác vẫn góp lợi nhuận từng phiên.
        var uni = duLieu
            .Where(d => TinhTv20(d.Bars) >= cfg.Tv20MinUni && d.Bars[^1].Close >= cfg.CloseMinUni)
            .Select(d => d.Symbol)
            .ToHashSet();

        var diem = TinhDiemTheoTruc(duLieu, uni, cfg);
        if (diem.Count < 2)
            return System.Array.Empty<DiemChuoi>();

        var muc = TinhMucChinhXep(diem);
        // Tổng lũy kế mức chỉ số để MA50 mỗi phiên là O(1).
        var prefixMuc = new decimal[muc.Count + 1];
        for (var i = 0; i < muc.Count; i++)
            prefixMuc[i + 1] = prefixMuc[i] + muc[i];

        var rows = new List<DiemChuoi>(muc.Count);
        for (var i = 0; i < muc.Count; i++)
        {
            var d = diem[i];
            if (tuNgay is { } tu && d.Ngay < tu)
                continue;
            var ma50 = SmaTuPrefix(prefixMuc, i, Ma50Window(i));
            var lech = ma50 > 0m ? (muc[i] / ma50 - 1m) * 100m : 0m;
            rows.Add(new DiemChuoi(
                d.Ngay,
                Math.Round(muc[i], 6),
                Math.Round(ma50, 6),
                Math.Round(lech, 4),
                d.LoiNhuan,
                d.SoMaUni,
                d.Breadth50,
                d.DanhSachUni));
        }
        return rows;
    }

    // ===== helpers chỉ báo =====

    private sealed record MaDuLieu(string Symbol, string Sector, string Exchange, List<OhlcvBar> Bars);

    /// <summary>TV20 = TB 20 phiên của close×volume / 10⁶ (tỷ đồng, theo research 01).</summary>
    private static decimal TinhTv20(IReadOnlyList<OhlcvBar> bars)
    {
        int n = Math.Min(20, bars.Count);
        if (n == 0)
            return 0m;
        decimal tong = 0m;
        for (var i = bars.Count - n; i < bars.Count; i++)
            tong += bars[i].Close * bars[i].Volume;
        return tong / n / 1_000_000m;
    }

    /// <summary>RET20 = close / close[−20] − 1, tính theo %.</summary>
    private static decimal TinhPhanTramLui(IReadOnlyList<decimal> closes, int lookback)
    {
        if (closes.Count <= lookback)
            return 0m;
        var now = closes[^1];
        var then = closes[closes.Count - 1 - lookback];
        if (then <= 0m)
            return 0m;
        return (now / then - 1m) * 100m;
    }

    /// <summary>Lợi nhuận % của close/close[−lookback] (dùng cho RS63/RS126).</summary>
    private static decimal TinhPhanTramLoiNhuan(IReadOnlyList<decimal> closes, int lookback) =>
        TinhPhanTramLui(closes, lookback);

    /// <summary>RSI14 làm mượt Wilder (alpha 1/14). Khác IndicatorMath.Rsi (cộng dồn).</summary>
    public static decimal RsiWilder(IReadOnlyList<decimal> closes, int period = 14)
    {
        if (closes.Count <= period)
            return 50m;

        decimal gainSum = 0m, lossSum = 0m;
        // Trung bình đơn 14 chênh lệch đầu (bước mồi của Wilder).
        for (var i = 1; i <= period; i++)
        {
            var d = closes[i] - closes[i - 1];
            if (d >= 0m) gainSum += d; else lossSum += -d;
        }
        decimal avgGain = gainSum / period;
        decimal avgLoss = lossSum / period;

        // Làm mượt Wilder cho các phiên còn lại.
        for (var i = period + 1; i < closes.Count; i++)
        {
            var d = closes[i] - closes[i - 1];
            var gain = d > 0m ? d : 0m;
            var loss = d < 0m ? -d : 0m;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss == 0m)
            return 100m;
        var rs = avgGain / avgLoss;
        return 100m - 100m / (1m + rs);
    }

    /// <summary>Đếm số phiên sàn trong `soPhien` phiên gần nhất (kể cả phiên cuối).</summary>
    private static int DemPhienSan(IReadOnlyList<decimal> closes, decimal bienDo, int soPhien)
    {
        var nguong = -(bienDo - 0.005m); // lợi nhuận ngày ≤ ngưỡng này = phiên sàn
        int dem = 0;
        int start = Math.Max(1, closes.Count - soPhien);
        for (var i = start; i < closes.Count; i++)
        {
            if (closes[i - 1] <= 0m) continue;
            var ret = closes[i] / closes[i - 1] - 1m;
            if (ret <= nguong)
                dem++;
        }
        return dem;
    }

    /// <summary>Phiên cuối (phiên tín hiệu) có phải phiên sàn không?</summary>
    private static bool LaPhienSan(IReadOnlyList<decimal> closes, decimal bienDo)
    {
        if (closes.Count < 2 || closes[^2] <= 0m)
            return false;
        var ret = closes[^1] / closes[^2] - 1m;
        return ret <= -(bienDo - 0.005m);
    }

    private static decimal? Sma(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period)
            return null;
        decimal tong = 0m;
        for (var i = closes.Count - period; i < closes.Count; i++)
            tong += closes[i];
        return tong / period;
    }

    private static decimal TrungVi(List<decimal> xs)
    {
        if (xs.Count == 0)
            return 0m;
        xs.Sort();
        int mid = xs.Count / 2;
        return xs.Count % 2 == 1 ? xs[mid] : (xs[mid - 1] + xs[mid]) / 2m;
    }

    /// <summary>Hạng phần trăm (0–100, cao = mạnh) của một tập giá trị trong UNI.</summary>
    private static Dictionary<string, decimal> HangPhanTramTrongUni(
        HashSet<string> uni,
        Dictionary<string, decimal> giaTri)
    {
        var ketQua = new Dictionary<string, decimal>();
        var ds = uni.Where(s => giaTri.ContainsKey(s))
                    .Select(s => (Symbol: s, V: giaTri[s]))
                    .OrderBy(x => x.V)
                    .ToList();
        if (ds.Count <= 1)
        {
            foreach (var x in ds) ketQua[x.Symbol] = 50m;
            return ketQua;
        }
        for (var i = 0; i < ds.Count; i++)
            ketQua[ds[i].Symbol] = (decimal)i / (ds.Count - 1) * 100m;
        return ketQua;
    }

    /// <summary>
    /// Dựng trục phiên chung và tính cho TỪNG phiên trên trục: lợi nhuận TB đã clip ±16%,
    /// số mã UNI + danh sách UNI, BREADTH50.
    /// BUSINESS-RULE (research 01, bt.py <c>R.where(UNI.shift())</c>): lợi nhuận tại phiên i
    /// chỉ lấy trên các mã <b>thuộc UNI tính tại phiên i−1</b> (TV20 ≥ ngưỡng và close ≥ ngưỡng
    /// cùng phiên đó) — KHÔNG dùng tập UNI của phiên tín hiệu cho cả lịch sử.
    /// Trục = union ngày của các mã thuộc UNI tại phiên tín hiệu (giữ như cũ).
    /// </summary>
    private static List<DiemTruc> TinhDiemTheoTruc(
        IReadOnlyList<MaDuLieu> duLieu,
        HashSet<string> uniTruc,
        V3CauHinh cfg)
    {
        var truc = new SortedSet<DateOnly>();
        var theoMa = new List<SeriesMaDuLieu>(duLieu.Count);
        foreach (var d in duLieu)
        {
            var dungTruc = uniTruc.Contains(d.Symbol);
            var n = d.Bars.Count;
            // Tổng lũy kế close và close×volume → MA50 và TV20 tại mọi phiên đều O(1).
            var prefixClose = new decimal[n + 1];
            var prefixDoanhThu = new decimal[n + 1];
            var closes = new Dictionary<DateOnly, decimal>(n);
            var uniTaiNgay = new Dictionary<DateOnly, bool>(n);
            var tranMa50TaiNgay = new Dictionary<DateOnly, bool>(n);
            for (var i = 0; i < n; i++)
            {
                var b = d.Bars[i];
                closes[b.Date] = b.Close;
                if (dungTruc)
                    truc.Add(b.Date);
                prefixClose[i + 1] = prefixClose[i] + b.Close;
                prefixDoanhThu[i + 1] = prefixDoanhThu[i] + b.Close * b.Volume;
                var tv20 = (prefixDoanhThu[i + 1] - prefixDoanhThu[Math.Max(0, i - 19)])
                    / (i + 1 - Math.Max(0, i - 19)) / 1_000_000m;
                uniTaiNgay[b.Date] = tv20 >= cfg.Tv20MinUni && b.Close >= cfg.CloseMinUni;
                // Cùng điều kiện Sma(closes, 50): cần đủ 50 nến, thiếu → chưa được tính là "vượt MA50".
                tranMa50TaiNgay[b.Date] = i + 1 >= 50 && b.Close > (prefixClose[i + 1] - prefixClose[i - 49]) / 50m;
            }
            theoMa.Add(new SeriesMaDuLieu(d.Symbol, closes, uniTaiNgay, tranMa50TaiNgay));
        }

        var dates = truc.ToList();
        var diem = new List<DiemTruc>(dates.Count);
        for (var i = 0; i < dates.Count; i++)
        {
            var homNay = dates[i];
            var loiNhuan = 0m;
            if (i > 0)
            {
                var qua = dates[i - 1];
                decimal tong = 0m;
                int soMa = 0;
                foreach (var m in theoMa)
                {
                    // Mã chỉ được tính ở phiên i nếu nó thuộc UNI tại phiên i−1 (UNI.shift()).
                    if (!m.Uni.TryGetValue(qua, out var thuocUniTruoc) || !thuocUniTruoc)
                        continue;
                    if (m.Closes.TryGetValue(qua, out var a)
                        && m.Closes.TryGetValue(homNay, out var b) && a > 0m)
                    {
                        tong += Math.Clamp(b / a - 1m, -0.16m, 0.16m);
                        soMa++;
                    }
                }
                loiNhuan = soMa > 0 ? tong / soMa : 0m;
            }

            // UNI và BREADTH50 của chính phiên này (UNI được lưu để dùng cho phiên kế tiếp).
            var dsUni = new List<string>();
            int coTranMa50 = 0;
            foreach (var m in theoMa)
            {
                if (m.Uni.TryGetValue(homNay, out var u) && u)
                {
                    dsUni.Add(m.Symbol);
                    if (m.TranMa50.TryGetValue(homNay, out var t) && t)
                        coTranMa50++;
                }
            }
            dsUni.Sort(StringComparer.OrdinalIgnoreCase);
            diem.Add(new DiemTruc(
                homNay,
                loiNhuan,
                dsUni.Count,
                dsUni,
                dsUni.Count > 0 ? Math.Round((decimal)coTranMa50 / dsUni.Count, 4) : 0m));
        }
        return diem;
    }

    private sealed record SeriesMaDuLieu(
        string Symbol,
        Dictionary<DateOnly, decimal> Closes,
        Dictionary<DateOnly, bool> Uni,
        Dictionary<DateOnly, bool> TranMa50);

    private sealed record DiemTruc(
        DateOnly Ngay,
        decimal LoiNhuan,
        int SoMaUni,
        IReadOnlyList<string> DanhSachUni,
        decimal Breadth50);

    /// <summary>Cấp số nhân chỉ số: mức[0] = 1 tại phiên đầu trục, mức[i] = mức[i−1] × (1 + lợi nhuận phiên i).</summary>
    private static List<decimal> TinhMucChinhXep(IReadOnlyList<DiemTruc> diem)
    {
        var muc = new List<decimal>(diem.Count) { 1m };
        for (var i = 1; i < diem.Count; i++)
            muc.Add(muc[^1] * (1m + diem[i].LoiNhuan));
        return muc;
    }

    /// <summary>Cửa sổ MA50 rút gọn khi chuỗi còn ngắn (min(50, số phiên đã có)) — giữ như bản cũ.</summary>
    private static int Ma50Window(int viTri) => Math.Min(50, viTri + 1);

    private static decimal SmaTuPrefix(decimal[] prefixMuc, int viTri, int period)
    {
        var dau = viTri + 1 - period;
        return (prefixMuc[viTri + 1] - prefixMuc[dau]) / period;
    }

    /// <summary>(mức cuối, MA50 cuối, % lệch, lợi nhuận phiên cuối) — phần chỉ số của <see cref="Tinh"/>.</summary>
    private static (decimal ChiSo, decimal Ma50, decimal DoLechPhanTram, decimal LoiNhuanHomNay) TinhMucCuoi(
        IReadOnlyList<DiemTruc> diem)
    {
        if (diem.Count < 2)
            return (1m, 1m, 0m, 0m);

        var muc = TinhMucChinhXep(diem);
        var prefix = new decimal[muc.Count + 1];
        for (var i = 0; i < muc.Count; i++)
            prefix[i + 1] = prefix[i] + muc[i];

        var last = muc.Count - 1;
        var ma50 = SmaTuPrefix(prefix, last, Ma50Window(last));
        var chiSo = muc[^1];
        var doLech = ma50 > 0m ? (chiSo / ma50 - 1m) * 100m : 0m;
        return (Math.Round(chiSo, 6), Math.Round(ma50, 6), Math.Round(doLech, 4), diem[^1].LoiNhuan);
    }
}
