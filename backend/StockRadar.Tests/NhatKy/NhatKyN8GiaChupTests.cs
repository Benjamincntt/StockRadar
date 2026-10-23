using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// N8 — giá tín hiệu chụp theo CHIỀU + THỨ TỰ lệnh trong cùng chu kỳ:
/// mua1→điểm 1, mua2→điểm 2; bán đầu→bán nửa (không có thì bán hết), bán sau→bán hết;
/// chưa có giá tương ứng → lệch null; đổi/xoá lệnh làm đổi thứ tự → chụp lại lệnh bị dịch bậc.
/// VIP (V1) đã ngừng nên không còn nguồn chụp giá riêng cho lệnh VIP cũ.
/// </summary>
public sealed class NhatKyN8GiaChupTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db) =>
        new(new EfNhatKyRepository(db), new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));

    private static string KeHoachJson(decimal min) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = min,
            GiaVaoLenhMax = min,
            GiaDungLo = min * 0.95m,
            GiaChotLoi1 = min * 1.10m,
            GiaChotLoi2 = min * 1.20m,
            DieuKienHuy = "test",
        });

    private static void SeedV2(
        ApplicationDbContext db, long id,
        decimal? giaVaoThucTe = 27_300m, decimal keHoachMin = 27_100m,
        decimal? giaMuaDiem2 = null, decimal? giaBanNua = null, decimal? giaThoatHet = null)
    {
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Id = id,
            Symbol = "HPG",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = T0,
            ThoiGianKichHoat = T0,
            GiaVaoThucTe = giaVaoThucTe,
            GiaMuaDiem2ThucTe = giaMuaDiem2,
            GiaBanNua = giaBanNua,
            GiaThoatHet = giaThoatHet,
            KeHoachGiaoDichJson = KeHoachJson(keHoachMin),
        });
        db.SaveChanges();
    }

    private static TaoLenhRequest Lenh(string chieu, DateTime thoiDiem, decimal gia, decimal kl, string ma = "341") =>
        new("HPG", chieu, thoiDiem, gia, kl, null, "V2", ma, null, null, null, null);

    private static async Task<LenhDto> MuaAsync(NhatKyService svc, Guid user, decimal gia, decimal kl, DateTime? t = null) =>
        await svc.TaoLenhAsync(user, Lenh("Mua", t ?? T0, gia, kl), default);

    private static async Task<LenhDto> BanAsync(NhatKyService svc, Guid user, decimal gia, decimal kl, DateTime t) =>
        await svc.TaoLenhAsync(user, Lenh("Ban", t, gia, kl), default);

    // ===== Mua: điểm 1 → điểm 2 =====

    [Fact]
    public async Task N8_MuaLanHaiGanCungChuKy_ChupGiaMuaDiem2()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaMuaDiem2: 28_500m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua1 = await MuaAsync(svc, user, 27_500m, 100);
        var mua2 = await MuaAsync(svc, user, 28_700m, 100, T0.AddDays(1));

        Assert.Equal(27_300m, mua1.GiaTinHieuChup);
        Assert.Equal(28_500m, mua2.GiaTinHieuChup);
        Assert.Equal(200m, mua2.LechSoTinHieu);
    }

    [Fact]
    public async Task N8_MuaLanBa_KhongConBacGia_LechNull()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaMuaDiem2: 28_500m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 100);
        await MuaAsync(svc, user, 28_700m, 100, T0.AddDays(1));
        var mua3 = await MuaAsync(svc, user, 29_000m, 100, T0.AddDays(2));

        Assert.Null(mua3.GiaTinHieuChup);
        Assert.Null(mua3.LechSoTinHieu);
    }

    // ===== Bán: đầu → bán nửa (hoặc bán hết), sau → bán hết =====

    [Fact]
    public async Task N8_BanDauTien_ChupGiaBanNua_BanSau_ChupGiaThoatHet()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaBanNua: 29_000m, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 1_000);
        var s1 = await BanAsync(svc, user, 29_200m, 500, T0.AddDays(1));
        var s2 = await BanAsync(svc, user, 30_500m, 500, T0.AddDays(2));

        Assert.Equal(29_000m, s1.GiaTinHieuChup);
        Assert.Equal(200m, s1.LechSoTinHieu);
        Assert.Equal(30_000m, s2.GiaTinHieuChup);
        Assert.Equal(500m, s2.LechSoTinHieu);
    }

    [Fact]
    public async Task N8_KhongCoBanNua_BanDauTien_ChupGiaThoatHet()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 500);
        var s1 = await BanAsync(svc, user, 30_200m, 500, T0.AddDays(1));

        Assert.Equal(30_000m, s1.GiaTinHieuChup);
    }

    [Fact]
    public async Task N8_ChuaCoGiaBan_LenhBanKhongCoLech()
    {
        await using var db = NewDb();
        SeedV2(db, 341); // chỉ có giá vào, chưa có nhịp bán
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua = await MuaAsync(svc, user, 27_500m, 500);
        var s1 = await BanAsync(svc, user, 28_900m, 500, T0.AddDays(1));

        Assert.Equal(27_300m, mua.GiaTinHieuChup);
        Assert.Null(s1.GiaTinHieuChup);
        Assert.Null(s1.LechSoTinHieu);
        Assert.Null(s1.LechSoTinHieuPhanTram);
    }

    // ===== Ví dụ lỗi chủ sản phẩm nêu: bán phải so với giá bán, không so giá mua =====

    [Fact]
    public async Task N8_LenhBanSoGiaBanKhongPhaiGiaMua_ViDuBug_RaTramNganChut()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m, giaBanNua: 29_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_300m, 1_000);
        var s1 = await BanAsync(svc, user, 28_900m, 1_000, T0.AddDays(1));

        // Trước N8 ra +1.600 (so sai giá mua 27.300); đúng phải là 28.900 − 29.000 = −100.
        Assert.Equal(-100m, s1.LechSoTinHieu);
        Assert.Equal(-0.34m, s1.LechSoTinHieuPhanTram);
    }

    // ===== Đổi thứ tự vì thêm/xoá/sửa lệnh → chụp lại lệnh bị dịch bậc =====

    [Fact]
    public async Task N8_XoaLenhBanDau_LenhConLaiChupLaiTheoThuTuMoi()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaBanNua: 29_000m, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 1_000);
        var s1 = await BanAsync(svc, user, 29_200m, 500, T0.AddDays(1));
        var s2 = await BanAsync(svc, user, 30_500m, 500, T0.AddDays(2));
        Assert.Equal(30_000m, s2.GiaTinHieuChup);

        Assert.True(await svc.XoaLenhAsync(user, s1.Id, default));

        var s2Sau = await svc.LayMotAsync(user, s2.Id, default);
        Assert.Equal(29_000m, s2Sau!.GiaTinHieuChup); // thành bán đầu tiên → so giá bán nửa
        Assert.Equal(1_500m, s2Sau.LechSoTinHieu);
    }

    [Fact]
    public async Task N8_ThemLenhMuaSomHon_LenhBiDich_ChupLaiMuaDiem2()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaMuaDiem2: 28_500m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var old = await MuaAsync(svc, user, 28_700m, 100, T0.AddDays(1));
        Assert.Equal(27_300m, old.GiaTinHieuChup);

        await MuaAsync(svc, user, 27_500m, 100, T0); // khớp sớm hơn → old dịch xuống bậc 2

        var oldSau = await svc.LayMotAsync(user, old.Id, default);
        Assert.Equal(28_500m, oldSau!.GiaTinHieuChup);
        Assert.Equal(200m, oldSau.LechSoTinHieu);
    }

    [Fact]
    public async Task N8_SuaThuNhoLenhLamDoiThuTu_ChupLaiCaHai()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaBanNua: 29_000m, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 1_000);
        var s1 = await BanAsync(svc, user, 29_200m, 500, T0.AddDays(1));
        var s2 = await BanAsync(svc, user, 30_500m, 500, T0.AddDays(2));

        // Kéo s2 về khớp ngay sau nhịp mua (trước s1) → s2 thành bán đầu (bán nửa), s1 thành bán sau (bán hết).
        await svc.SuaLenhAsync(user, s2.Id, Lenh("Ban", T0.AddHours(4), 30_500m, 500), default);

        var s1Sau = await svc.LayMotAsync(user, s1.Id, default);
        var s2Sau = await svc.LayMotAsync(user, s2.Id, default);
        Assert.Equal(30_000m, s1Sau!.GiaTinHieuChup);
        Assert.Equal(29_000m, s2Sau!.GiaTinHieuChup);
    }

    [Fact]
    public async Task N8_SuaGiaKhopKhongDoiThuTu_GiuGiaDaChup()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaBanNua: 29_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 500);
        var s1 = await BanAsync(svc, user, 29_200m, 500, T0.AddDays(1));

        var sua = await svc.SuaLenhAsync(user, s1.Id, Lenh("Ban", T0.AddDays(1), 29_800m, 500), default);
        Assert.Equal(29_000m, sua!.GiaTinHieuChup); // bậc không đổi → không đọc lại nguồn
        Assert.Equal(800m, sua.LechSoTinHieu);
    }

    // ===== Lệch bình quân chỉ gộp lệnh CÙNG CHIỀU + CÙNG mức giá chụp =====

    [Fact]
    public async Task N8_LechBinhQuan_ChiGopLenhCungChieuCungMucChup()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_000m, giaBanNua: 29_000m, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua = await MuaAsync(svc, user, 27_000m, 1_000);
        var s1 = await BanAsync(svc, user, 28_900m, 500, T0.AddDays(1));   // bậc bán 1 → so 29.000
        await BanAsync(svc, user, 30_500m, 250, T0.AddDays(2));            // bậc bán 2 → nhóm BQ
        var s3 = await BanAsync(svc, user, 31_000m, 250, T0.AddDays(3));   // bậc bán 3 → nhóm BQ

        Assert.Equal(0m, mua.LechSoTinHieu);
        Assert.Equal(-100m, s1.LechSoTinHieu); // không bị trộn với các lệnh bán bậc sau

        // BQ hai lệnh bán bậc "bán hết" = (30.500×250 + 31.000×250)/500 = 30.750 → lệch +750.
        var s3Sau = await svc.LayMotAsync(user, s3.Id, default);
        Assert.Equal(750m, s3Sau!.LechSoTinHieu);
    }
}
