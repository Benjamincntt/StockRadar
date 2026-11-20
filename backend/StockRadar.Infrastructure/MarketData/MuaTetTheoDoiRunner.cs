using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19): đọc giỏ ĐÃ MUA từ Nhật ký lệnh của tài khoản V3:MuaTet:TaiKhoan
/// (nguồn MUATET, đợt 2c), không có thì từ cấu hình V3:MuaTet:GioDaMua; lấy giá hiện tại từ Stocks.LastClose
/// và giá tham chiếu phiên trước, tính tỷ lệ so vốn bằng MuaTetTheoDoi (hàm thuần)
/// rồi gửi tin cảnh báo 95% / bán cả giỏ 90%. Không ghi bảng, không gọi KBS, không phụ thuộc V3:Enabled.
/// </summary>
internal sealed class MuaTetTheoDoiRunner(
    IGiaHienTaiRepository gia,
    IGuiThongBao guiTin,
    MuaTetChanLap chanLap,
    IOptions<MuaTetOptions> cauHinh,
    ILogger<MuaTetTheoDoiRunner> logger,
    IGioMuaTetNhatKy gioNhatKy) : IMuaTetTheoDoiService
{
    public async Task<MuaTetTheoDoiDto> LayTrangThaiAsync(CancellationToken ct = default) =>
        await TinhAsync(ct);

    public async Task<MuaTetTheoDoiDto> KiemVaBaoAsync(bool boQuaChanLap = false, CancellationToken ct = default)
    {
        var cauHinhHienTai = cauHinh.Value;
        var trangThai = await TinhAsync(ct);

        if (!cauHinhHienTai.TheoDoiBat)
        {
            logger.LogInformation("Mùa Tết theo dõi: TheoDoiBat = false — bỏ qua, không gửi tin.");
            return trangThai with { ThongDiep = "TheoDoiBat = false — bỏ qua." };
        }

        if (trangThai.CacMa.Count == 0)
        {
            logger.LogInformation("Mùa Tết theo dõi: giỏ đã mua trống — chưa có gì để theo dõi.");
            return trangThai with { ThongDiep = "Giỏ đã mua trống (V3:MuaTet:GioDaMua) — chưa có gì để theo dõi." };
        }

        var muc = Enum.Parse<MuaTetTheoDoi.MucBao>(trangThai.Muc);
        logger.LogInformation(
            "Mùa Tết theo dõi: {SoMa} mã, tỷ lệ {TyLe:P2}, mức {Muc}, thiếu giá {ThieuGia}.",
            trangThai.CacMa.Count, trangThai.TyLe, muc, trangThai.SoMaThieuGia);

        if (muc == MuaTetTheoDoi.MucBao.BinhThuong)
            return trangThai with { ThongDiep = "Trên ngưỡng cảnh báo — không gửi tin." };

        var homNay = VietnamMarketCalendar.TodayVietnam();
        if (!boQuaChanLap && chanLap.DaGui(homNay, muc))
        {
            logger.LogInformation("Mùa Tết theo dõi: mức {Muc} đã gửi trong ngày {Ngay} — bỏ qua.", muc, homNay);
            return trangThai with { ThongDiep = $"Mức {muc} đã gửi hôm nay — chặn lặp." };
        }

        await guiTin.GuiHeThongAsync(MuaTetTheoDoiTinNhan.TieuDeDay(muc), MuaTetTheoDoiTinNhan.Dung(trangThai), ct);
        chanLap.DanhDau(homNay, muc);

        logger.LogInformation(
            "Mùa Tết theo dõi: đã gửi tin mức {Muc}, tỷ lệ {TyLe:P2}, giỏ {GiaTri} / vốn {Von} đồng.",
            muc, trangThai.TyLe, trangThai.GiaTriHienTai, trangThai.VonDaMua);

        return trangThai with { DaGuiTin = true, ThongDiep = $"Đã gửi tin mức {muc}." };
    }

    private async Task<MuaTetTheoDoiDto> TinhAsync(CancellationToken ct)
    {
        var cauHinhHienTai = cauHinh.Value;
        var (gio, ngayMua, nguonGio) = await LayGioDaMuaAsync(cauHinhHienTai, ct);

        var maCanGia = gio
            .Where(m => !string.IsNullOrWhiteSpace(m.Symbol) && m.GiaVao > 0m && m.SoCo > 0)
            .Select(m => m.Symbol)
            .ToList();

        var giaHienTai = maCanGia.Count == 0
            ? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            : await gia.LayLastCloseAsync(maCanGia, ct);

        var thamChieu = maCanGia.Count == 0
            ? new Dictionary<string, GiaThamChieuPhien>(StringComparer.OrdinalIgnoreCase)
            : await gia.LayGiaThamChieuAsync(maCanGia, ct);
        var giaThamChieu = thamChieu.ToDictionary(
            kv => kv.Key.ToUpperInvariant(), kv => kv.Value.GiaThamChieu, StringComparer.OrdinalIgnoreCase);
        DateOnly? ngayPhien = thamChieu.Count == 0 ? null : thamChieu.Values.Max(t => t.NgayPhien);

        var ketQua = MuaTetTheoDoi.Tinh(
            gio, giaHienTai, cauHinhHienTai.NguongCanhBao, cauHinhHienTai.NguongBan, giaThamChieu);

        var ds = new List<MuaTetTheoDoiMaDto>(ketQua.ChiTiet.Count);
        foreach (var m in ketQua.ChiTiet)
        {
            ds.Add(new MuaTetTheoDoiMaDto(
                m.Symbol, m.GiaVao, m.SoCo, m.GiaHienTai, m.ThieuGia,
                m.VonDong, m.GiaTriDong, m.LaiLoPhanTram,
                m.LaiLoDong, m.GiaThamChieu, m.ThayDoiTrongNgayPhanTram, m.LaiLoTrongNgayDong));
        }

        return new MuaTetTheoDoiDto(
            cauHinhHienTai.TheoDoiBat,
            ngayMua,
            ketQua.VonDong,
            ketQua.GiaTriDong,
            ketQua.TyLe,
            ketQua.Muc.ToString(),
            ds,
            ketQua.MaKeoXuongNhieuNhat,
            ketQua.SoMaThieuGia,
            DaGuiTin: false,
            ThongDiep: null,
            LaiLoDong: ketQua.LaiLoDong,
            NgayPhien: ngayPhien,
            LaiLoTrongNgayDong: ketQua.LaiLoTrongNgayDong,
            ThayDoiTrongNgayPhanTram: ketQua.ThayDoiTrongNgayPhanTram,
            MaMua: cauHinhHienTai.MaMua,
            NguonGio: nguonGio);
    }

    /// <summary>
    /// Giỏ đã mua: ưu tiên Nhật ký lệnh của tài khoản cấu hình (nguồn MUATET đúng mã mùa);
    /// tài khoản trống / sai dạng hoặc chưa có lệnh của mùa thì dùng giỏ ghi tay trong cấu hình.
    /// </summary>
    /// <param name="cauHinhHienTai">Cấu hình V3:MuaTet đang dùng.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Giỏ, ngày mua (giờ Việt Nam) và nguồn giỏ (NhatKy / CauHinh).</returns>
    private async Task<(List<MuaTetTheoDoi.MaDaMua> Gio, DateOnly? NgayMua, string Nguon)> LayGioDaMuaAsync(
        MuaTetOptions cauHinhHienTai, CancellationToken ct)
    {
        if (Guid.TryParse(cauHinhHienTai.TaiKhoan, out var taiKhoan)
            && !string.IsNullOrWhiteSpace(cauHinhHienTai.MaMua))
        {
            var tuNhatKy = await gioNhatKy.LayGioMuaTetAsync(taiKhoan, cauHinhHienTai.MaMua.Trim(), ct);
            if (tuNhatKy.Count > 0)
            {
                var gio = tuNhatKy
                    .Select(m => new MuaTetTheoDoi.MaDaMua(
                        m.Symbol, Math.Round(m.GiaVonBinhQuan, 4), (int)decimal.Floor(m.KhoiLuongMo)))
                    .ToList();
                var ngayMua = DateOnly.FromDateTime(VietnamMarketCalendar.ToVietnam(tuNhatKy.Min(m => m.MuaDauUtc)));
                return (gio, ngayMua, "NhatKy");
            }
        }
        else if (!string.IsNullOrWhiteSpace(cauHinhHienTai.TaiKhoan))
        {
            logger.LogWarning("Mùa Tết theo dõi: V3:MuaTet:TaiKhoan không phải Guid hợp lệ — dùng giỏ trong cấu hình.");
        }

        var gioCauHinh = cauHinhHienTai.GioDaMua
            .Select(m => new MuaTetTheoDoi.MaDaMua(m.Symbol, m.GiaVao, m.SoCo))
            .ToList();
        return (gioCauHinh, cauHinhHienTai.NgayMua, "CauHinh");
    }
}
