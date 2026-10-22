namespace StockRadar.Domain.Services;

/// <summary>DO-NOT-CHANGE: 1 lệnh nhật ký đưa vào projection; phí đã quy về % hiệu dụng ở tầng service (mua: BuyFeePercent, bán: BanFeeTaxPercent = phí + thuế).</summary>
public sealed record LenhBinhQuan(
    string Chieu,
    DateTime ThoiDiem,
    decimal Gia,
    decimal KL,
    decimal BuyFeePercent,
    decimal? KLieuChinh,
    decimal? VonDieuChinh,
    decimal BanFeeTaxPercent = 0m);

/// <summary>US3: nhịp bán ghi lúc đi tuần — giá vốn BQ đúng tại nhịp để tầng service tính lãi ròng bằng RealizedPnlMath (không copy công thức).</summary>
public sealed record NhipBanBinhQuan(DateOnly Ngay, decimal GiaBan, decimal KhoiLuong, decimal GiaVonTaiNhip, decimal BanFeeTaxPercent);

/// <summary>Kết quả đi tuần theo thời điểm: vị thế hiện tại + tồn kho mở tại một thời điểm kiểm tra.</summary>
public sealed record DuongDiBinhQuan(
    decimal KhoiLuongMo,
    decimal GiaVonBinhQuan,
    decimal TienVonDangKe,
    DateOnly? NgayMo,
    int SoLenhDaBan,
    bool HopLe,
    decimal MoTaiThoiDiem,
    IReadOnlyList<NhipBanBinhQuan>? NhipBan = null);

/// <summary>
/// BUSINESS-RULE: giá vốn bình quân gia quyền khối lượng (Q5) — nhịp bán không đổi giá vốn phần còn lại;
/// Điều chỉnh (+/-KL, +/-VND mỗi cp) chỉ tác động tồn kho và giá vốn (N7). Pure, không DB.
/// </summary>
public static class TinhGiaVonBinhQuan
{
    /// <summary>Đi tuần lệnh theo ThoiDiem tăng dần. <paramref name="tai"/> (kèm <paramref name="danhDauTai"/>) = tồn kho ngay sau lệnh được đánh dấu đó.</summary>
    public static DuongDiBinhQuan Tinh(
        IReadOnlyList<LenhBinhQuan> lenh,
        DateTime? tai = null,
        int danhDauTai = -1)
    {
        var indexed = lenh
            .Select((l, i) => (l, i))
            .OrderBy(x => x.l.ThoiDiem)
            .ThenBy(x => x.i)
            .ToList();

        var open = 0m;
        var tongVon = 0m;
        var soBan = 0;
        var hopLe = true;
        DateOnly? ngayMo = null;
        var moTai = 0m;
        var taiDaXet = false;
        var nhipBan = new List<NhipBanBinhQuan>();

        foreach (var (l, i) in indexed)
        {
            if (tai.HasValue && !taiDaXet && l.ThoiDiem > tai.Value)
            {
                moTai = open;
                taiDaXet = true;
            }

            switch (l.Chieu)
            {
                case "Mua":
                {
                    var buyCost = l.Gia * (1 + l.BuyFeePercent / 100m);
                    tongVon += l.KL * buyCost;
                    open += l.KL;
                    ngayMo ??= DateOnly.FromDateTime(l.ThoiDiem);
                    break;
                }
                case "Ban":
                {
                    if (open <= 0 || l.KL > open)
                    {
                        hopLe = false;
                        break;
                    }
                    // Bán theo giá vốn BQ hiện hành: vốn rút = KL bán × BQ; phần còn lại giữ nguyên BQ.
                    var avgTruoc = open > 0 ? tongVon / open : 0m;
                    if (avgTruoc > 0m)
                        nhipBan.Add(new NhipBanBinhQuan(DateOnly.FromDateTime(l.ThoiDiem), l.Gia, l.KL, avgTruoc, l.BanFeeTaxPercent));
                    tongVon -= l.KL * avgTruoc;
                    open -= l.KL;
                    soBan++;
                    if (open == 0) tongVon = 0m;
                    break;
                }
                case "DieuChinh":
                {
                    var newOpen = open + (l.KLieuChinh ?? 0m);
                    if (newOpen < 0)
                    {
                        hopLe = false;
                        break;
                    }
                    tongVon += (l.VonDieuChinh ?? 0m) * Math.Max(newOpen, 0m);
                    if (tongVon < 0) tongVon = 0m;
                    if (newOpen == 0) tongVon = 0m;
                    open = newOpen;
                    break;
                }
                default:
                    hopLe = false;
                    break;
            }

            if (!hopLe) break;

            if (tai.HasValue && !taiDaXet && i == danhDauTai)
            {
                moTai = open;
                taiDaXet = true;
            }
        }

        if (tai.HasValue && !taiDaXet)
            moTai = open;

        if (tai.HasValue)
            return new DuongDiBinhQuan(moTai, 0m, 0m, null, 0, hopLe, moTai);

        var avg = open > 0 ? tongVon / open : 0m;
        var von = Math.Round(open * avg, 2, MidpointRounding.AwayFromZero);
        return new DuongDiBinhQuan(open, Math.Round(avg, 4, MidpointRounding.AwayFromZero), von, ngayMo, soBan, hopLe, open, nhipBan);
    }
}
