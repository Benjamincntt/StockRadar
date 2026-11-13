using Microsoft.EntityFrameworkCore;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.MarketData;

/// <summary>
/// Spec 017 T007 — kho nến 1 phút chỉ một dòng mỗi mã mỗi phiên, và sổ chất lượng phân biệt
/// đủ / thiếu / rỗng / lỗi để job 17:00 không ghi rỗng nhầm khi KBS lỗi.
/// </summary>
public sealed class NenPhutRepositoryTests
{
    private static readonly DateOnly Phien = new(2026, 11, 12);
    private const int NguongThieuNen = 200;

    private static ApplicationDbContext TaoDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static KbsHistoryClient.KbsChartBar Nen(int phut, decimal gia) =>
        new(new DateTimeOffset(2026, 11, 12, phut / 60, phut % 60, 0, TimeSpan.Zero),
            gia, gia + 0.05m, gia - 0.05m, gia + 0.01m, 1_000 + phut);

    /// <summary>Phiên 226 nến: 09:15–11:29, 13:00–14:29, cộng nến ATC ở phút truyền vào.</summary>
    private static List<KbsHistoryClient.KbsChartBar> Phien226(int phutNenAtc)
    {
        var bars = new List<KbsHistoryClient.KbsChartBar>();
        for (var p = 9 * 60 + 15; p <= 11 * 60 + 29; p++)
            bars.Add(Nen(p, 30.10m));
        for (var p = 13 * 60; p <= 14 * 60 + 29; p++)
            bars.Add(Nen(p, 30.20m));
        bars.Add(Nen(phutNenAtc, 30.30m));
        return bars;
    }

    private static List<KbsHistoryClient.KbsChartBar> PhienNgan(int soNen)
    {
        var bars = new List<KbsHistoryClient.KbsChartBar>();
        for (var p = 9 * 60 + 15; p < 9 * 60 + 15 + soNen; p++)
            bars.Add(Nen(p, 30.10m));
        return bars;
    }

    private static KbsKetQuaTai KetQuaOk(IReadOnlyList<KbsHistoryClient.KbsChartBar> bars) =>
        new(bars, KbsTrangThaiTai.Ok);

    [Fact]
    public async Task LuuNenTheoPhien_GoiHaiLanCungMaPhien_ChiMotDong_BlockMoiNhat()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (nen1440, _) = NenPhutChatLuong.PhanLoai(
            "hpg", Phien, KetQuaOk(Phien226(14 * 60 + 40)), NguongThieuNen, 1, "Job");
        await repo.LuuNenTheoPhienAsync(nen1440!);

        var (nen1445, _) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, KetQuaOk(Phien226(14 * 60 + 45)), NguongThieuNen, 2, "Job");
        await repo.LuuNenTheoPhienAsync(nen1445!);

        Assert.Equal(1, await db.NenPhutTheoPhien.CountAsync());
        var docLai = await repo.GetNenTheoPhienAsync("HPG", Phien);
        Assert.NotNull(docLai);
        Assert.Equal("HPG", docLai!.Symbol);
        Assert.Equal(226, docLai.SoNen);
        Assert.Equal(14 * 60 + 45, NenPhutCodec.GiaiNen(docLai.DuLieuNen)[^1].Phut);
    }

    [Fact]
    public async Task PhanLoai_226Nen_TraDu_VaGhiGioAtcThatCuaPhien()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (dongNen, dongChatLuong) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, KetQuaOk(Phien226(14 * 60 + 45)), NguongThieuNen, 1, "Job");

        Assert.Equal(NenPhutChatLuong.Du, dongChatLuong.TrangThai);
        Assert.Equal(226, dongChatLuong.SoNenNhanDuoc);
        Assert.Equal(new TimeOnly(14, 45), dongChatLuong.GioNenAtc);
        Assert.Null(dongChatLuong.ThongDiepLoi);
        Assert.NotNull(dongNen);

        await repo.LuuChatLuongAsync(dongChatLuong);
        var docLai = await repo.GetChatLuongAsync("hpg", Phien);
        Assert.Equal(NenPhutChatLuong.Du, docLai!.TrangThai);
        Assert.Equal(new TimeOnly(14, 45), docLai.GioNenAtc);
    }

    [Fact]
    public async Task PhanLoai_120Nen_TraThieu()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (dongNen, dongChatLuong) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, KetQuaOk(PhienNgan(120)), NguongThieuNen, 1, "Job");

        Assert.Equal(NenPhutChatLuong.Thieu, dongChatLuong.TrangThai);
        Assert.Equal(120, dongChatLuong.SoNenNhanDuoc);
        Assert.NotNull(dongNen);

        await repo.LuuChatLuongAsync(dongChatLuong);
        Assert.Equal(NenPhutChatLuong.Thieu, (await repo.GetChatLuongAsync("HPG", Phien))!.TrangThai);
    }

    [Fact]
    public async Task PhanLoai_KbsRong_TraRong_KhongCoKhoiNen()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (dongNen, dongChatLuong) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, new KbsKetQuaTai([], KbsTrangThaiTai.Rong), NguongThieuNen, 1, "Job");

        Assert.Equal(NenPhutChatLuong.Rong, dongChatLuong.TrangThai);
        Assert.Equal(0, dongChatLuong.SoNenNhanDuoc);
        Assert.Null(dongChatLuong.GioNenAtc);
        Assert.Null(dongNen);

        await repo.LuuChatLuongAsync(dongChatLuong);
        Assert.Equal(0, await db.NenPhutTheoPhien.CountAsync());
        Assert.Equal(1, await db.SoChatLuongNenPhut.CountAsync());
    }

    [Fact]
    public async Task PhanLoai_HttpError_TraLoi_CoThongDiepLoi()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (dongNen, dongChatLuong) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, new KbsKetQuaTai([], KbsTrangThaiTai.HttpError, "HTTP 500"),
            NguongThieuNen, 2, "Job");

        Assert.Equal(NenPhutChatLuong.Loi, dongChatLuong.TrangThai);
        Assert.Equal("HTTP 500", dongChatLuong.ThongDiepLoi);
        Assert.Equal(2, dongChatLuong.SoLanThu);
        Assert.Null(dongNen);

        await repo.LuuChatLuongAsync(dongChatLuong);
        var docLai = await repo.GetChatLuongAsync("HPG", Phien);
        Assert.Equal(NenPhutChatLuong.Loi, docLai!.TrangThai);
        Assert.Equal("HTTP 500", docLai.ThongDiepLoi);
    }

    [Fact]
    public async Task LuuChatLuong_GoiHaiLanCungMaPhien_ChiMotDong_TrangThaiMoiNhat()
    {
        var db = TaoDb();
        var repo = new EfNenPhutRepository(db);

        var (_, lanMot) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, KetQuaOk(PhienNgan(120)), NguongThieuNen, 1, "Job");
        await repo.LuuChatLuongAsync(lanMot);

        var (_, lanHai) = NenPhutChatLuong.PhanLoai(
            "HPG", Phien, KetQuaOk(Phien226(14 * 60 + 40)), NguongThieuNen, 2, "Job");
        await repo.LuuChatLuongAsync(lanHai);

        var theoPhien = await repo.GetChatLuongTheoPhienAsync(Phien);
        var dong = Assert.Single(theoPhien);
        Assert.Equal(NenPhutChatLuong.Du, dong.TrangThai);
        Assert.Equal(226, dong.SoNenNhanDuoc);
        Assert.Equal(2, dong.SoLanThu);
    }
}
