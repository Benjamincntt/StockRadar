using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho Pha 3 — C-27 (Phase 9b): chấm theo CHU KỲ, không đo một lần sau 4 ngày.
/// P1: chỉ chấm khi chu kỳ đóng (ThoiGianThoatHet != null); đang mở → KetQuaDoLuong giữ null ("Chờ đo").
/// P2: giá vào = giá khớp (bình quân hai lần mua khi có điểm 2); giá ra = bình quân trọng số các lần bán.
/// P3: dòng còn mở mà mang kết quả cũ (do cách đo cũ để lại) → xoá về null, chờ đóng.
/// Dòng "Huy" (HetHanVao / HuyNgoaiVung / HuyMuaLap) không chấm, không ghi đè.
/// </summary>
public sealed class Pha3DoLuongTests
{
    // ===== Helpers =====

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Pha3DoLuongRunner TaoRunner(ApplicationDbContext db) =>
        new(db, NullLogger<Pha3DoLuongRunner>.Instance);

    private static string KeHoachJson(decimal entry, decimal sl, decimal tp1) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = entry,
            GiaVaoLenhMax = entry,
            GiaDungLo = sl,
            GiaChotLoi1 = tp1,
            GiaChotLoi2 = tp1,
            DieuKienHuy = "Test"
        });

    /// <summary>Dòng đã kích hoạt, chu kỳ CÒN MỞ (mặc định 10 ngày trước — quá xa mốc 4 ngày cũ).</summary>
    private static KetQuaKichBanEntity TaoEntity(
        string symbol, string keHoachJson, int soNgayTruoc = 10) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = DateTime.UtcNow.AddDays(-soNgayTruoc),
            ThoiGianKichHoat = DateTime.UtcNow.AddDays(-soNgayTruoc),
            KeHoachGiaoDichJson = keHoachJson,
        };

    /// <summary>Đóng chu kỳ tại UTC xác định (mặc định hôm qua 07:00 UTC ≈ 14:00 VN).</summary>
    private static KetQuaKichBanEntity Dong(
        KetQuaKichBanEntity e, decimal giaThoatHet,
        string lyDo = SuKienBan.LyDoDungLo,
        DateTime? thoiGianThoatHet = null)
    {
        e.GiaThoatHet = giaThoatHet;
        var homQua = DateTime.UtcNow.AddDays(-1);
        e.ThoiGianThoatHet = thoiGianThoatHet
            ?? new DateTime(homQua.Year, homQua.Month, homQua.Day, 7, 0, 0, DateTimeKind.Utc);
        e.LyDoThoatHet = lyDo;
        return e;
    }

    private static async Task<KetQuaKichBanEntity> LuuAsync(ApplicationDbContext db, KetQuaKichBanEntity e)
    {
        db.KetQuaKichBan.Add(e);
        await db.SaveChangesAsync();
        return e;
    }

    // ===== T064 (P1): mở quá 4 ngày vẫn KHÔNG chấm — regression bug PVS =====

    [Fact]
    public async Task T064_ChuKyMo_HonBonNgay_VanKhongCham()
    {
        var db = NewDb();
        // PVS-style: mua 33.60, nến đóng cửa hiện tại cao — cách đo cũ sẽ chấm "Thắng" dù còn mở.
        var entity = await LuuAsync(db, TaoEntity("PVS", KeHoachJson(33.6m, 31.0m, 40m), soNgayTruoc: 10));

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);                          // "Chờ đo" — UI giữ nhãn C-28
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Null(entity.GiaThoat);
    }

    // ===== T065 (P2): đóng sau bán nửa + bán hết → giá ra bình quân trọng số =====

    [Fact]
    public async Task T065_DongSauBanNuaVaBanHet_GiaRaBinhQuanHaiGia()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)), 100m, SuKienBan.LyDoDungLo));   // quay về giá vào bằng dừng lỗ đã dời
        entity.GiaVaoThucTe = 100m;
        entity.GiaBanNua      = 110m;
        entity.ThoiGianBanNua = DateTime.UtcNow.AddDays(-3);
        entity.DaDoiDungLo    = true;
        await db.SaveChangesAsync();

        var ketQua = await TaoRunner(db).RunAsync();

        // giá ra = (110 + 100) / 2 = 105 → +5% ≥ 1% → Thang → ChotLoi
        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal(105m, entity.GiaThoat);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.ChotLoi, entity.TrangThai);
        Assert.Equal(5.0000m, entity.PhanTramLoiNhuan);
        Assert.Equal(1.0000m, entity.TyLeLaiLoThucTe);              // (105−100)/(100−95)
    }

    [Fact]
    public async Task T065_DongChiBanHet_GiaRaLaGiaThoatHet()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)), 93m, SuKienBan.LyDoDungLo));
        entity.GiaVaoThucTe = 100m;

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(1, ketQua.Thua);
        Assert.Equal(93m, entity.GiaThoat);
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.HuyLenh, entity.TrangThai);   // giữ hành vi đổi TrangThai theo kết quả đo
        Assert.Equal(-7.0000m, entity.PhanTramLoiNhuan);
        Assert.Equal(-1.4000m, entity.TyLeLaiLoThucTe);
    }

    [Fact]
    public async Task T065_BanNuaRoiRutMoc6_BinhQuanVanThua()
    {
        // Bán nửa vì rút mốc 4% ở 96, bán hết RutMoc6 ở 94 → ra = 95 → −5% → Thua.
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)), 94m, SuKienBan.LyDoRutMoc6));
        entity.GiaVaoThucTe = 100m;
        entity.GiaBanNua    = 96m;
        entity.DinhTuLucMua = 100m;
        await db.SaveChangesAsync();

        await TaoRunner(db).RunAsync();

        Assert.Equal(95m, entity.GiaThoat);
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(-5.0000m, entity.PhanTramLoiNhuan);
    }

    // ===== T066 (P2): có điểm 2 → giá vào bình quân hai lần mua =====

    [Fact]
    public async Task T066_CoDiem2_GiaVaoBinhQuanHaiLanMua()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 95m, 130m)), 120m, SuKienBan.LyDoDungLoDuoi));
        entity.GiaVaoThucTe      = 100m;   // điểm 1
        entity.GiaMuaDiem2ThucTe = 110m;   // điểm 2 → bình quân vào = 105
        entity.ThoiGianMuaDiem2  = DateTime.UtcNow.AddDays(-4);
        await db.SaveChangesAsync();

        await TaoRunner(db).RunAsync();

        // ra = 120 (chỉ một lần bán hết) → (120−105)/105 = +14.2857% → Thang
        Assert.Equal(120m, entity.GiaThoat);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(14.2857m, entity.PhanTramLoiNhuan);
        Assert.Equal(1.5000m, entity.TyLeLaiLoThucTe);              // (120−105)/(105−95)
    }

    // ===== T067 (PVS-style): mua 33.60, cắt 31.50, chu kỳ đóng → Thua =====

    [Fact]
    public async Task T067_PvsStyle_DongCungCatLo_ChamThuaDung()
    {
        var db = NewDb();
        // PVS thật (C-26): mua 33.60, bị cắt 31.50 — cách đo cũ chấm "Thắng" 12/10 rồi giữ nguyên.
        var entity = await LuuAsync(db, Dong(
            TaoEntity("PVS", KeHoachJson(33.6m, 31.0m, 37m)), 31.50m, SuKienBan.LyDoDungLo,
            thoiGianThoatHet: new DateTime(2026, 10, 16, 2, 30, 0, DateTimeKind.Utc)));
        entity.GiaVaoThucTe = 33.60m;

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(1, ketQua.Thua);
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.HuyLenh, entity.TrangThai);
        Assert.Equal(31.50m, entity.GiaThoat);
        Assert.Equal(-6.2500m, entity.PhanTramLoiNhuan);            // (31.5−33.6)/33.6
        Assert.Equal(new DateOnly(2026, 10, 16), entity.NgayThoat); // 09:30 giờ VN ngày 16/10
    }

    // ===== NgayThoat = ngày ThoiGianThoatHet theo giờ VN =====

    [Fact]
    public async Task NgayThoat_LaNgayThoatHetDoTheoGioVN()
    {
        var db = NewDb();
        // 17:30 UTC 14/10 = 00:30 giờ VN 15/10 → ngayThoat phải là 15/10, không phải 14/10.
        var entity = await LuuAsync(db, Dong(
            TaoEntity("FRT", KeHoachJson(100m, 95m, 110m)), 100.5m, SuKienBan.LyDoDungLo,
            thoiGianThoatHet: new DateTime(2026, 10, 14, 17, 30, 0, DateTimeKind.Utc)));
        entity.GiaVaoThucTe = 100m;

        await TaoRunner(db).RunAsync();

        Assert.Equal(new DateOnly(2026, 10, 15), entity.NgayThoat);
        Assert.Equal("Ngang", entity.KetQuaDoLuong);                // +0.5% trong ngưỡng ±1%
    }

    // ===== Dòng 'Huy' — không chấm, không ghi đè =====

    [Fact]
    public async Task DongHuy_KhongChamKhongGhiDe()
    {
        var db = NewDb();
        // C-10 style: dòng mua lặp bị script đóng với KetQuaDoLuong='Huy', không có giá thoát.
        var entity = await LuuAsync(db, TaoEntity("ABB", KeHoachJson(100m, 95m, 110m)));
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);
        entity.LyDoThoatHet     = SuKienBan.LyDoHuyMuaLap;
        entity.KetQuaDoLuong    = "Huy";
        await db.SaveChangesAsync();

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Equal("Huy", entity.KetQuaDoLuong);
        Assert.Null(entity.GiaThoat);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
    }

    // ===== T069 (P3): dòng còn mở mang kết quả cũ → xoá về null, chờ đóng =====

    [Fact]
    public async Task T069_P3_DongMoConKetQuaCu_XoaVeChoDo()
    {
        var db = NewDb();
        // Di sản cách đo cũ: PVS 2168 từng bị chấm "Thang" khi còn mở, chu kỳ vẫn chưa đóng.
        var entity = await LuuAsync(db, TaoEntity("PVS", KeHoachJson(33.6m, 31.0m, 40m)));
        entity.KetQuaDoLuong    = "Thang";
        entity.GiaThoat         = 36m;
        entity.PhanTramLoiNhuan = 7.14m;
        entity.TrangThai        = TrangThaiKichBan.ChotLoi;
        await db.SaveChangesAsync();   // di sản cách đo cũ phải nằm trong DB thì RunAsync mới dọn

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);                         // không chấm lại (vẫn mở)
        Assert.Null(entity.KetQuaDoLuong);                            // xoá về "Chờ đo"
        Assert.Equal(36m, entity.GiaThoat);                           // giá thoát cũ không bị đụng tới
        Assert.Equal(TrangThaiKichBan.ChotLoi, entity.TrangThai);     // P3 chỉ xoá KetQuaDoLuong
    }

    [Fact]
    public async Task T069_P3_KhongXoaDongHuy()
    {
        var db = NewDb();
        // HetHanVao: mở (chưa từng vào) nhưng KetQuaDoLuong='Huy' — P3 không được xoá.
        var entity = await LuuAsync(db, TaoEntity("NTP", KeHoachJson(52.8m, 50m, 56m)));
        entity.TrangThai     = TrangThaiKichBan.HuyLenh;
        entity.KetQuaDoLuong = "Huy";
        entity.LyDoThoatHet  = SuKienBan.HetHanVao;
        await db.SaveChangesAsync();

        await TaoRunner(db).RunAsync();

        Assert.Equal("Huy", entity.KetQuaDoLuong);
    }

    // ===== Dòng đã đóng VÀ đã có kết quả → bỏ qua, không đo lại =====

    [Fact]
    public async Task DongVaDaCoKetQua_KhongDoLai()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)), 93m));
        entity.KetQuaDoLuong    = "Thang";   // kết quả cũ — dòng đã đóng có kết quả không được chấm lại
        entity.GiaThoat         = 105m;
        await db.SaveChangesAsync();

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(105m, entity.GiaThoat);
    }

    // ===== ±1%: Ngang không đổi TrangThai; entry == SL → R:R null =====

    [Fact]
    public async Task Kiem1PhanTram_TrongNguong_Ngang_GiuTrangThai()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)), 100.5m, SuKienBan.LyDoHetThoiGian));
        entity.GiaVaoThucTe = 100m;

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(1, ketQua.Ngang);
        Assert.Equal("Ngang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
    }

    [Fact]
    public async Task EntryBangSl_RrNull_KhongCrash()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(
            TaoEntity("HPG", KeHoachJson(100m, 100m, 110m)), 103m, SuKienBan.LyDoDungLoDuoi));
        entity.GiaVaoThucTe = 100m;

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Null(entity.TyLeLaiLoThucTe);
    }

    // ===== Kế hoạch JSON hỏng / thiếu giá vào → bỏ qua, không crash =====

    [Fact]
    public async Task KeHoachJsonHong_BoQua_KhongDo()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, Dong(TaoEntity("HPG", "{json-hong:::"), 93m));

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);
    }

    // ===== Dòng đóng thiếu giá thoát (dữ liệu bất thường) → bỏ qua, chờ tay người =====

    [Fact]
    public async Task DongThieuGiaThoat_BoQua_KhongCham()
    {
        var db = NewDb();
        var entity = await LuuAsync(db, TaoEntity("HPG", KeHoachJson(100m, 95m, 110m)));
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);   // đóng nhưng không có GiaThoatHet
        entity.LyDoThoatHet     = SuKienBan.LyDoDungLo;
        await db.SaveChangesAsync();

        var ketQua = await TaoRunner(db).RunAsync();

        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);
    }

    // ===== T013 (giữ nguyên): huỷ lệnh chờ vào quá hạn của ngày đánh giá =====

    [Fact]
    public async Task DangHinhThanhCuaHomNay_BiHuyLyDoHetHanVao()
    {
        var db = NewDb();
        var homNay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        var entityHomNay = await LuuAsync(db, new KetQuaKichBanEntity
        {
            Symbol = "FORM_TODAY",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = homNay,
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = 80m,
        });
        var entityNgayCu = await LuuAsync(db, new KetQuaKichBanEntity
        {
            Symbol = "FORM_OLD",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = homNay.AddDays(-2),
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = 80m,
        });

        await TaoRunner(db).RunAsync();

        // Lệnh chờ vào của phiên đánh giá chỉ sống hết phiên — quá hạn bị huỷ.
        Assert.Equal(TrangThaiKichBan.HuyLenh, entityHomNay.TrangThai);
        Assert.Equal("Huy", entityHomNay.KetQuaDoLuong);
        Assert.Equal(SuKienBan.HetHanVao, entityHomNay.LyDoThoatHet);

        // Dòng ngày cũ không bị động — Pha 3 chỉ huỷ đúng hạn của ngày đánh giá hôm nay.
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, entityNgayCu.TrangThai);
        Assert.Null(entityNgayCu.KetQuaDoLuong);
        Assert.Null(entityNgayCu.LyDoThoatHet);
    }
}
