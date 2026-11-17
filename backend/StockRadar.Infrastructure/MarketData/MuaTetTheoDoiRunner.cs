using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19) — đợt 2a: đọc giỏ ĐÃ MUA từ cấu hình <c>V3:MuaTet:GioDaMua</c>,
/// lấy giá hiện tại từ cột <c>Stocks.LastClose</c>, tính tỷ lệ so vốn bằng <see cref="MuaTetTheoDoi"/> (hàm thuần)
/// rồi gửi tin cảnh báo 95% / bán cả giỏ 90%. Không ghi bảng, không gọi KBS, không phụ thuộc <c>V3:Enabled</c>.
/// </summary>
internal sealed class MuaTetTheoDoiRunner(
    IGiaHienTaiRepository gia,
    IGuiThongBao guiTin,
    MuaTetChanLap chanLap,
    IOptions<MuaTetOptions> cauHinh,
    ILogger<MuaTetTheoDoiRunner> logger) : IMuaTetTheoDoiService
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
        var gio = new List<MuaTetTheoDoi.MaDaMua>(cauHinhHienTai.GioDaMua.Count);
        foreach (var m in cauHinhHienTai.GioDaMua)
            gio.Add(new MuaTetTheoDoi.MaDaMua(m.Symbol, m.GiaVao, m.SoCo));

        var maCanGia = gio
            .Where(m => !string.IsNullOrWhiteSpace(m.Symbol) && m.GiaVao > 0m && m.SoCo > 0)
            .Select(m => m.Symbol)
            .ToList();

        var giaHienTai = maCanGia.Count == 0
            ? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            : await gia.LayLastCloseAsync(maCanGia, ct);

        var ketQua = MuaTetTheoDoi.Tinh(gio, giaHienTai, cauHinhHienTai.NguongCanhBao, cauHinhHienTai.NguongBan);

        var ds = new List<MuaTetTheoDoiMaDto>(ketQua.ChiTiet.Count);
        foreach (var m in ketQua.ChiTiet)
        {
            ds.Add(new MuaTetTheoDoiMaDto(
                m.Symbol, m.GiaVao, m.SoCo, m.GiaHienTai, m.ThieuGia,
                m.VonDong, m.GiaTriDong, m.LaiLoPhanTram));
        }

        return new MuaTetTheoDoiDto(
            cauHinhHienTai.TheoDoiBat,
            cauHinhHienTai.NgayMua,
            ketQua.VonDong,
            ketQua.GiaTriDong,
            ketQua.TyLe,
            ketQua.Muc.ToString(),
            ds,
            ketQua.MaKeoXuongNhieuNhat,
            ketQua.SoMaThieuGia,
            DaGuiTin: false,
            ThongDiep: null);
    }
}
