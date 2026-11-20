using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4): các mã gần đỉnh 1 năm trong top 100 thanh khoản, giới hạn số mã
/// mỗi ngành (mặc định 10 mã / 2 mã ngành; cấu hình được, xem specs/020-mua-tet/spec.md). Mua giữa tháng 11,
/// giữ tới giữa tháng 3, bán cả giỏ khi giá trị giỏ ≤ 90% vốn.
/// Hàm THUẦN: đầu vào là mã + nến + ngành đã nạp sẵn — không gọi DB, không gọi mạng.
/// </summary>
public static class MuaTetChonGio
{
    /// <summary>Số mã mặc định của giỏ.</summary>
    public const int SoMa = 10;

    /// <summary>Vũ trụ theo thanh khoản: lấy 100 mã có TV20 cao nhất.</summary>
    public const int TopThanhKhoan = 100;

    /// <summary>Số mã tối đa mặc định của một ngành trong giỏ.</summary>
    public const int ToiDaMoiNganh = 2;

    /// <summary>Số phiên để xét đỉnh và trung vị giá (đỉnh 1 năm), tính cả phiên gần nhất.</summary>
    public const int SoPhienXet = 250;

    /// <summary>Số phiên tính thanh khoản trung bình.</summary>
    public const int SoPhienThanhKhoan = 20;

    /// <summary>Giá đóng cửa tối thiểu, đơn vị nghìn đồng.</summary>
    public const decimal GiaDongCuaMin = 3m;

    /// <summary>Giá trị giao dịch TB 20 phiên tối thiểu, đơn vị tỷ đồng.</summary>
    public const decimal Tv20MinTy = 1m;

    /// <summary>Số cổ phiếu một lô.</summary>
    public const int LoCoPhieu = 100;

    /// <summary>Mã có 1 lô đắt hơn hệ số này × giá trị một chỗ thì bỏ khỏi giỏ, lấy mã kế tiếp.</summary>
    public const decimal HeSoVuotCho = 1.5m;

    /// <summary>Ngưỡng giá nghi lẫn đơn vị (nghìn đồng → đồng) trong cửa sổ xét.</summary>
    public const decimal NguongGiaLanDonVi = 500m;

    /// <summary>Trung vị đóng cửa dưới mốc này (nghìn đồng) thì coi là dữ liệu lẫn đơn vị.</summary>
    public const decimal TrungViNghiLanDonViMax = 100m;

    /// <summary>Giờ đóng cửa phiên — trước mốc này nến hôm nay chưa xong.</summary>
    public static readonly TimeOnly GioDongCua = new(15, 0);

    /// <summary>Một mã đầu vào: mã, ngành, toàn bộ nến ngày đã lưu.</summary>
    public sealed record MaDauVao(string Symbol, string Sector, IReadOnlyList<OhlcvBar> Bars);

    /// <summary>Một mã được chọn, đủ số liệu để in tin và đối chiếu.</summary>
    /// <param name="GanDinh">Giá đóng cửa phiên gần nhất ÷ giá cao nhất của <see cref="SoPhienXet"/> phiên (0–1).</param>
    /// <param name="GiaTriGiaoDichTb20Ty">TB 20 phiên của close × volume ÷ 10^6, tỷ đồng.</param>
    /// <param name="GiaMotLoDong">1 lô (100 cổ) × giá đóng cửa, đơn vị đồng (giá lưu bằng nghìn đồng).</param>
    /// <param name="SoLoGoiY">Số lô gợi ý cho một chỗ vốn, tối thiểu 1.</param>
    public sealed record MaDuocChon(
        string Symbol,
        string Sector,
        decimal GiaDongCua,
        decimal GanDinh,
        decimal GiaTriGiaoDichTb20Ty,
        decimal GiaMotLoDong,
        int SoLoGoiY);

    /// <summary>Kết quả chọn giỏ của một ngày dữ liệu.</summary>
    /// <param name="BiLoaiLanDonVi">Mã bị loại vì giá lưu lẫn đơn vị — in ra để truy vết.</param>
    /// <param name="BiBoVuotCho">Mã đủ điểm vào giỏ nhưng bị bỏ vì 1 lô vượt <see cref="HeSoVuotCho"/> lần một chỗ.</param>
    public sealed record KetQuaChon(
        DateOnly NgayCat,
        decimal VonBanDau,
        decimal GiaTriMotChoDong,
        IReadOnlyList<MaDuocChon> Gio,
        IReadOnlyList<string> BiLoaiLanDonVi,
        IReadOnlyList<string> BiBoVuotCho);

    /// <summary>
    /// Ngày nến cuối cùng được phép dùng. Hôm nay chỉ được tính khi là ngày giao dịch và
    /// phiên đã đóng (<see cref="GioDongCua"/>); ngược lại lùi về phiên giao dịch trước.
    /// </summary>
    /// <param name="homNay">Ngày theo giờ Việt Nam.</param>
    /// <param name="gioHienTai">Giờ theo giờ Việt Nam.</param>
    /// <returns>Ngày của nến hoàn thành gần nhất.</returns>
    public static DateOnly NgayCatNen(DateOnly homNay, TimeOnly gioHienTai) =>
        TradingSessionMath.IsTradingDay(homNay) && gioHienTai >= GioDongCua
            ? homNay
            : TradingSessionMath.SubtractTradingSessions(homNay, 1);

    /// <summary>
    /// Chọn giỏ: lọc vũ trụ → 100 mã TV20 cao nhất → xếp theo gần đỉnh giảm dần,
    /// lấy tối đa soMa mã và tối đa toiDaMoiNganh mã mỗi ngành; bỏ mã có 1 lô vượt chỗ, lấy mã kế tiếp.
    /// </summary>
    /// <param name="cacMa">Toàn bộ mã cần xét (đã lọc active ở nơi gọi).</param>
    /// <param name="ngayCat">Nến sau ngày này bị bỏ (xem NgayCatNen).</param>
    /// <param name="vonBanDau">Vốn cả giỏ, đơn vị đồng. Vốn chia đều cho soMa chỗ.</param>
    /// <param name="loCoPhieu">Số cổ phiếu một lô.</param>
    /// <param name="soMa">Số mã của giỏ; ≤ 0 thì dùng 10.</param>
    /// <param name="toiDaMoiNganh">Số mã tối đa một ngành; ≤ 0 thì dùng 2.</param>
    /// <returns>Giỏ tối đa soMa mã kèm số liệu, và danh sách mã bị loại vì lẫn đơn vị.</returns>
    public static KetQuaChon Chon(
        IReadOnlyList<MaDauVao> cacMa,
        DateOnly ngayCat,
        decimal vonBanDau,
        int loCoPhieu = LoCoPhieu,
        int soMa = SoMa,
        int toiDaMoiNganh = ToiDaMoiNganh)
    {
        var soMaGio = soMa > 0 ? soMa : SoMa;
        var toiDaNganh = toiDaMoiNganh > 0 ? toiDaMoiNganh : ToiDaMoiNganh;

        // BUSINESS-RULE: vốn cả giỏ chia đều cho số mã của giỏ (owner chốt giỏ 5 mã ngày 20/11/2026).
        var giaTriMotChoDong = vonBanDau > 0m ? vonBanDau / soMaGio : 0m;
        var lo = loCoPhieu > 0 ? loCoPhieu : LoCoPhieu;

        var ungVien = new List<UngVien>();
        var lanDonVi = new List<string>();

        foreach (var ma in cacMa)
        {
            var tatCa = LayCuaSo(ma.Bars, ngayCat, int.MaxValue);
            if (tatCa.Count < SoPhienXet)
                continue;

            var cuaSo = tatCa.Count == SoPhienXet
                ? tatCa
                : tatCa.GetRange(tatCa.Count - SoPhienXet, SoPhienXet);

            var giaCaoNhat = decimal.MinValue;
            foreach (var nen in cuaSo)
            {
                if (nen.High > giaCaoNhat)
                    giaCaoNhat = nen.High;
            }

            // LIMIT: giá lưu lẫn đơn vị (970 thay vì 0,97) làm gan_dinh và TV20 sai hoàn toàn — loại hẳn mã.
            // Trung vị lấy trên TOÀN BỘ lịch sử (chỉ nến < 500) như script nghiên cứu: mã đã "lên đơn vị đồng"
            // trọn 250 phiên cuối vẫn bị bắt, vì trung vị của riêng cửa sổ khi đó đã ≥ 100.
            if (giaCaoNhat >= NguongGiaLanDonVi && TrungViDuoi500(tatCa) is { } trungVi
                && trungVi < TrungViNghiLanDonViMax)
            {
                lanDonVi.Add(ma.Symbol);
                continue;
            }

            var close = cuaSo[^1].Close;
            if (close < GiaDongCuaMin || giaCaoNhat <= 0m)
                continue;

            var tv20 = TinhTv20(cuaSo);
            if (tv20 < Tv20MinTy)
                continue;

            ungVien.Add(new UngVien(ma.Symbol, ma.Sector ?? "", close, close / giaCaoNhat, tv20));
        }

        // Thanh khoản quyết định vũ trụ, hòa thì theo mã để kết quả tất định.
        var top = ungVien
            .OrderByDescending(u => u.Tv20)
            .ThenBy(u => u.Symbol, StringComparer.Ordinal)
            .Take(TopThanhKhoan);

        var xepHang = top
            .OrderByDescending(u => u.GanDinh)
            .ThenByDescending(u => u.Tv20)
            .ThenBy(u => u.Symbol, StringComparer.Ordinal);

        var demNganh = new Dictionary<string, int>(StringComparer.Ordinal);
        var gio = new List<MaDuocChon>(soMaGio);
        var boVuotCho = new List<string>();
        foreach (var u in xepHang)
        {
            if (gio.Count >= soMaGio)
                break;

            // BUSINESS-RULE: tối đa toiDaNganh mã/ngành — giỏ 10 mã thì 2 (P4-19), giỏ 5 mã thì 1 (backtest 20/11/2026).
            var daCo = demNganh.TryGetValue(u.Sector, out var so) ? so : 0;
            if (daCo >= toiDaNganh)
                continue;

            var giaMotLoDong = u.Close * lo * 1000m;

            // BUSINESS-RULE: owner chốt 17/11/2026 — 1 lô > 1,5 × một chỗ thì bỏ mã, lấy mã kế tiếp (không chiếm suất ngành).
            if (giaTriMotChoDong > 0m && giaMotLoDong > HeSoVuotCho * giaTriMotChoDong)
            {
                boVuotCho.Add(u.Symbol);
                continue;
            }

            demNganh[u.Sector] = daCo + 1;

            var soLo = giaTriMotChoDong > 0m && giaMotLoDong > 0m
                ? (int)Math.Max(1m, Math.Round(giaTriMotChoDong / giaMotLoDong, MidpointRounding.AwayFromZero))
                : 1;

            gio.Add(new MaDuocChon(
                u.Symbol,
                u.Sector,
                u.Close,
                Math.Round(u.GanDinh, 4),
                Math.Round(u.Tv20, 2),
                Math.Round(giaMotLoDong, 0),
                soLo));
        }

        return new KetQuaChon(ngayCat, vonBanDau, Math.Round(giaTriMotChoDong, 0), gio, lanDonVi, boVuotCho);
    }

    /// <summary>
    /// Lấy tối đa <paramref name="soPhien"/> nến cuối cùng có ngày ≤ <paramref name="ngayCat"/>, xếp tăng dần theo ngày.
    /// </summary>
    private static List<OhlcvBar> LayCuaSo(IReadOnlyList<OhlcvBar> bars, DateOnly ngayCat, int soPhien)
    {
        if (bars.Count == 0)
            return [];

        var loc = new List<OhlcvBar>(bars.Count);
        foreach (var bar in bars)
        {
            if (bar.Date <= ngayCat)
                loc.Add(bar);
        }

        if (loc.Count == 0)
            return loc;

        loc.Sort((a, b) => a.Date.CompareTo(b.Date));
        if (loc.Count <= soPhien)
            return loc;

        return loc.GetRange(loc.Count - soPhien, soPhien);
    }

    /// <summary>TV20 = TB 20 phiên của close × volume ÷ 10^6 (tỷ đồng) — cùng công thức V3.</summary>
    private static decimal TinhTv20(IReadOnlyList<OhlcvBar> cuaSo)
    {
        var n = Math.Min(SoPhienThanhKhoan, cuaSo.Count);
        if (n == 0)
            return 0m;

        decimal tong = 0m;
        for (var i = cuaSo.Count - n; i < cuaSo.Count; i++)
            tong += cuaSo[i].Close * cuaSo[i].Volume;

        return tong / n / 1_000_000m;
    }

    private static decimal TrungVi(List<decimal> ds)
    {
        ds.Sort();
        var giua = ds.Count / 2;
        return ds.Count % 2 == 0 ? (ds[giua - 1] + ds[giua]) / 2m : ds[giua];
    }

    /// <summary>Trung vị đóng cửa của các nến dưới 500 (bỏ nến đã ở đơn vị đồng); null khi không có nến nào.</summary>
    private static decimal? TrungViDuoi500(IReadOnlyList<OhlcvBar> tatCa)
    {
        var ds = new List<decimal>(tatCa.Count);
        foreach (var nen in tatCa)
        {
            if (nen.Close < NguongGiaLanDonVi)
                ds.Add(nen.Close);
        }

        return ds.Count == 0 ? null : TrungVi(ds);
    }

    private sealed record UngVien(string Symbol, string Sector, decimal Close, decimal GanDinh, decimal Tv20);
}
