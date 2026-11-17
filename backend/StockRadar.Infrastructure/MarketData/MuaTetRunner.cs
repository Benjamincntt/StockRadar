using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4) — đợt 1: đọc nến ngày của các mã active trong DB,
/// chọn giỏ bằng <see cref="MuaTetChonGio"/> (hàm thuần), bắn MỘT tin qua kênh thông báo V3.
/// Không ghi bảng, không tạo vị thế, không gọi KBS/FireAnt.
/// </summary>
internal sealed class MuaTetRunner(
    IStockRepository kho,
    IGuiThongBao guiTin,
    IOptions<MuaTetOptions> cauHinh,
    ILogger<MuaTetRunner> logger) : IMuaTetService
{
    private const string TenKichBan = "Mùa Tết — mã khỏe (P4-19)";

    public async Task<MuaTetGioDto> LayGioAsync(CancellationToken ct = default)
    {
        // Nến hôm nay chỉ được dùng khi phiên đã đóng; trước đó lấy phiên giao dịch trước.
        var now = VietnamMarketCalendar.NowVietnam();
        var ngayCat = MuaTetChonGio.NgayCatNen(
            DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now));

        var cacMa = await kho.GetAllAsync(ct);
        var dauVao = new List<MuaTetChonGio.MaDauVao>(cacMa.Count);
        foreach (var ma in cacMa)
        {
            if (ma.History is null || ma.History.Count < MuaTetChonGio.SoPhienXet)
                continue;
            dauVao.Add(new MuaTetChonGio.MaDauVao(ma.Symbol, ma.Sector ?? "", ma.History));
        }

        var ketQua = MuaTetChonGio.Chon(dauVao, ngayCat, cauHinh.Value.VonBanDau);
        logger.LogInformation(
            "Mùa Tết: xét {SoMa} mã, chọn {Chon} mã, dữ liệu tới {Ngay}, loại lẫn đơn vị {LanDonVi}, bỏ vượt chỗ {VuotCho}.",
            dauVao.Count, ketQua.Gio.Count, ketQua.NgayCat, ketQua.BiLoaiLanDonVi.Count, ketQua.BiBoVuotCho.Count);

        var ds = new List<MuaTetMaDto>(ketQua.Gio.Count);
        for (var i = 0; i < ketQua.Gio.Count; i++)
        {
            var m = ketQua.Gio[i];
            ds.Add(new MuaTetMaDto(
                i + 1, m.Symbol, m.Sector, m.GiaDongCua, m.GanDinh,
                m.GiaTriGiaoDichTb20Ty, m.GiaMotLoDong, m.SoLoGoiY));
        }

        return new MuaTetGioDto(
            TenKichBan, ketQua.NgayCat, ketQua.VonBanDau, ketQua.GiaTriMotChoDong,
            ds, ketQua.BiLoaiLanDonVi, ketQua.BiBoVuotCho);
    }

    public async Task<MuaTetGioDto> BaoTinAsync(CancellationToken ct = default)
    {
        var gio = await LayGioAsync(ct);
        await guiTin.GuiHeThongAsync(MuaTetTinNhan.TieuDeDay, MuaTetTinNhan.Dung(gio), ct);
        logger.LogInformation("Mùa Tết: đã gửi tin {Chon} mã, dữ liệu tới {Ngay}.", gio.CacMa.Count, gio.NgayDuLieu);
        return gio;
    }
}
