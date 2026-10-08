using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho Pha 3 — Pha3DoLuongRunner.
/// Kiểm thử: phân loại Thắng/Thua/Ngang theo TP1/SL/ngưỡng ±1%,
/// bộ lọc thời gian chờ đo (4 ngày lịch), bỏ qua bản ghi đã đo,
/// tính % lợi nhuận và R:R thực tế (kể cả entry == SL).
/// </summary>
public sealed class Pha3DoLuongTests
{
    // ===== Fake repository (giống mẫu trong Pha2TrongPhienTests) =====

    private sealed class FakeStockRepository : IJobStockRepository
    {
        private readonly Dictionary<string, Stock> _stocks = new(StringComparer.OrdinalIgnoreCase);

        public void ThemStock(string symbol, IReadOnlyList<OhlcvBar> history) =>
            _stocks[symbol] = new Stock(symbol, symbol, "Test", history);

        public Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(_stocks.TryGetValue(symbol, out var s) ? s : null);

        public Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Stock>>(_stocks.Values.ToList());

        public Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_stocks.Keys.ToList());

        public Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
            IReadOnlyList<string> symbols, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StockSummaryRow>>(
                symbols.Where(s => _stocks.ContainsKey(s))
                    .Select(s => new StockSummaryRow(s, s, "Test", false, 0m)).ToList());

        public Task<MarketBreadthStats> GetBreadthStatsAsync(CancellationToken ct = default) =>
            Task.FromResult(MarketBreadthStats.Empty);

        public Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Stock>>(_stocks.Values.ToList());

        public Task<IReadOnlyList<string>> GetInactiveSymbolsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    // ===== Helpers =====

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Pha3DoLuongRunner TaoRunner(FakeStockRepository repo, ApplicationDbContext db) =>
        new(repo, db, NullLogger<Pha3DoLuongRunner>.Instance);

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

    /// <summary>Entity đã kích hoạt, đủ thời gian chờ đo (mặc định 5 ngày trước).</summary>
    private static KetQuaKichBanEntity TaoEntity(
        string symbol, string keHoachJson, int soNgayTruoc = 5) =>
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

    /// <summary>Nến đóng cửa tại ngày hôm nay với giá đóng cửa xác định.</summary>
    private static IReadOnlyList<OhlcvBar> NenDongCua(decimal close)
    {
        var d = DateOnly.FromDateTime(DateTime.UtcNow);
        return new List<OhlcvBar>
        {
            new(d.AddDays(-1), close, close, close, close, 1_000_000),
            new(d, close, close, close, close, 1_000_000),
        };
    }

    // ===== Test 1: Chạm TP1 → Thang / ChotLoi =====

    [Fact]
    public async Task ChamTp1_DoLaThang_ChuyenChotLoi()
    {
        // Arrange: entry=100, SL=95, TP1=110, giá hiện tại=112 (≥ TP1)
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(112m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal(1, ketQua.Thang);
        Assert.Equal(0, ketQua.Thua);
        Assert.Equal(0, ketQua.Ngang);

        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.ChotLoi, entity.TrangThai);
        Assert.Equal(112m, entity.GiaThoat);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), entity.NgayThoat);
        Assert.Equal(12.0000m, entity.PhanTramLoiNhuan);       // (112−100)/100 × 100
        Assert.Equal(2.4000m, entity.TyLeLaiLoThucTe);         // (112−100)/(100−95)
    }

    // ===== Test 2: Chạm SL → Thua / HuyLenh =====

    [Fact]
    public async Task ChamSl_DoLaThua_ChuyenHuyLenh()
    {
        // Arrange: entry=100, SL=95, TP1=110, giá hiện tại=93 (≤ SL)
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(93m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal(1, ketQua.Thua);

        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.HuyLenh, entity.TrangThai);
        Assert.Equal(93m, entity.GiaThoat);
        Assert.Equal(-7.0000m, entity.PhanTramLoiNhuan);       // (93−100)/100 × 100
        Assert.Equal(-1.4000m, entity.TyLeLaiLoThucTe);        // (93−100)/(100−95)
    }

    // ===== Test 3: Chưa chạm TP1/SL, lãi ≥ 1% → Thang =====

    [Fact]
    public async Task KhongChamTp1Sl_LaiTren1PhanTram_DoLaThang()
    {
        // Arrange: giá=103 → +3% (không chạm TP1=110, không chạm SL=95)
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(103m));

        // Act
        await TaoRunner(repo, db).RunAsync();

        // Assert: trạng thái giữ nguyên DaKichHoat, chỉ phân loại kết quả
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Equal(3.0000m, entity.PhanTramLoiNhuan);
        Assert.Equal(0.6000m, entity.TyLeLaiLoThucTe);         // (103−100)/(100−95)
    }

    // ===== Test 4: Chưa chạm TP1/SL, lỗ ≥ 1% → Thua =====

    [Fact]
    public async Task KhongChamTp1Sl_LoTren1PhanTram_DoLaThua()
    {
        // Arrange: giá=97 → −3%
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(97m));

        // Act
        await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Equal(-3.0000m, entity.PhanTramLoiNhuan);
        Assert.Equal(-0.6000m, entity.TyLeLaiLoThucTe);        // (97−100)/(100−95)
    }

    // ===== Test 5: Chưa chạm TP1/SL, trong ±1% → Ngang =====

    [Fact]
    public async Task KhongChamTp1Sl_Trong1PhanTram_DoLaNgang()
    {
        // Arrange: giá=100.5 → +0.5% (trong ngưỡng ±1%)
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(100.5m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(1, ketQua.Ngang);
        Assert.Equal("Ngang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Equal(0.5000m, entity.PhanTramLoiNhuan);
    }

    // ===== Test 6: Bộ lọc thời gian chờ đo (≥ 4 ngày lịch) =====

    [Fact]
    public async Task KichHoat2Ngay_ChuaDenHan_KhongDo()
    {
        // Arrange: kích hoạt 2 ngày trước (< 4 ngày chờ) → chưa đến hạn đo
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m), soNgayTruoc: 2);
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(112m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert: không đo gì, bản ghi giữ nguyên
        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
    }

    [Fact]
    public async Task KichHoat5Ngay_DenHan_DuocDo()
    {
        // Arrange: kích hoạt 5 ngày trước (≥ 4 ngày chờ) → đến hạn đo
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m), soNgayTruoc: 5);
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(112m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
    }

    // ===== Test 7: Bản ghi đã đo → bỏ qua =====

    [Fact]
    public async Task DaDoTruocDo_BoQua_KhongDoLai()
    {
        // Arrange: đã có kết quả đo "Thang" từ trước
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.KetQuaDoLuong = "Thang";
        entity.GiaThoat = 105m;
        entity.PhanTramLoiNhuan = 5m;
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(93m)); // giá sập sâu nhưng KHÔNG được đo lại

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert: không đo lại, giá trị cũ giữ nguyên
        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(105m, entity.GiaThoat);
        Assert.Equal(5m, entity.PhanTramLoiNhuan);
    }

    // ===== Test 8: entry == SL → R:R null, không crash =====

    [Fact]
    public async Task EntryBangSl_RrNull_KhongCrash()
    {
        // Arrange: SL trùng entry → rủi ro = 0, không được chia 0
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 100m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(103m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert: vẫn phân loại bình thường, R:R = null
        Assert.Equal(1, ketQua.TongDoLuong);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Null(entity.TyLeLaiLoThucTe);
        Assert.Equal(3.0000m, entity.PhanTramLoiNhuan);
    }

    // ===== Test phụ: không có nến → bỏ qua, không đo =====

    [Fact]
    public async Task KhongCoNen_BoQua_KhongDo()
    {
        // Arrange: symbol không có dữ liệu trong repo
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository(); // không thêm stock nào

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);
    }

    // ===== Test phụ: kế hoạch JSON hỏng → bỏ qua, không đo =====

    [Fact]
    public async Task KeHoachJsonHong_BoQua_KhongDo()
    {
        // Arrange: JSON không parse được
        var db = NewDb();
        var entity = TaoEntity("HPG", "{json-hong:::");
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(112m));

        // Act
        var ketQua = await TaoRunner(repo, db).RunAsync();

        // Assert
        Assert.Equal(0, ketQua.TongDoLuong);
        Assert.Null(entity.KetQuaDoLuong);
    }

    // ===== Test TC7: Pha 3 — Pha 2 exit price avg (GiaBanNua + GiaThoatHet) =====

    [Fact]
    public async Task Pha3_CoGiaBanNuaVaThoatHet_TinhTrungBinh()
    {
        // entry=100, SL=95, TP1=110
        // Pha 2 ghi: TP1 at 110 (GiaBanNua=110, DaDoiDungLo=true), SL at 100 (GiaThoatHet=100, LyDoThoatHet="DungLo")
        // Expected: giaThoat = (110 + 100) / 2 = 105 → +5% → Thang
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.GiaBanNua      = 110m;
        entity.ThoiGianBanNua = DateTime.UtcNow.AddDays(-3);
        entity.DaDoiDungLo    = true;
        entity.GiaThoatHet     = 100m;
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);
        entity.LyDoThoatHet    = "DungLo";
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(103m)); // không được dùng vì có exit prices

        // Act
        await TaoRunner(repo, db).RunAsync();

        // Assert: giaThoat = (110+100)/2 = 105; pct = +5% >= 1% → Thang → ChotLoi
        Assert.Equal(105m, entity.GiaThoat);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.ChotLoi, entity.TrangThai);
        Assert.Equal(5.0000m, entity.PhanTramLoiNhuan); // (105-100)/100*100
    }

    [Fact]
    public async Task Pha3_CoGiaThoatHetKhongGiaBanNua_TinhGiaThoatHet()
    {
        // entry=100, SL=95. Pha2 ghi SL exit at 93 (LyDoThoatHet="DungLo", DaDoiDungLo=false → Thua)
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.GiaThoatHet     = 93m;
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);
        entity.LyDoThoatHet    = "DungLo";
        // DaDoiDungLo = false (default)
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(93m));

        // Act
        await TaoRunner(repo, db).RunAsync();

        // Assert: giaThoat = GiaThoatHet = 93; pct = -7% <= -1% → Thua → HuyLenh
        Assert.Equal(93m, entity.GiaThoat);
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.HuyLenh, entity.TrangThai);
    }

    [Fact]
    public async Task Pha3_CoGiaBanNuaKhongThoatHet_TinhAvgGiaBanNuaVaClose()
    {
        // entry=100, SL=95, TP1=110. Pha2 ghi KietSuc at 110 (banNua only, no ThoatHet)
        // Expected: giaThoat = (110 + close=103) / 2 = 106.5
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.GiaBanNua      = 110m;
        entity.ThoiGianBanNua = DateTime.UtcNow.AddDays(-3);
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(103m));

        // Act
        await TaoRunner(repo, db).RunAsync();

        // Assert: giaThoat = (110 + 103) / 2 = 106.5; pct = +6.5% >= 1% → Thang → ChotLoi
        Assert.Equal(106.5m, entity.GiaThoat);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.ChotLoi, entity.TrangThai);
    }

    // Ca 10: Pha2 exit prices, ±1% threshold (không còn nhánh DungLo riêng)

    [Fact]
    public async Task Pha3_DungLoExit_TinhTheo1PhanTram()
    {
        // GiaThoatHet=94, giaVao=100 → pct=-6% ≤ -1% → Thua → HuyLenh
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.GiaThoatHet     = 94m;
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);
        entity.LyDoThoatHet    = "DungLo";
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(94m));

        await TaoRunner(repo, db).RunAsync();

        Assert.Equal(94m, entity.GiaThoat);
        Assert.Equal("Thua", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.HuyLenh, entity.TrangThai);
        Assert.Equal(-6.0000m, entity.PhanTramLoiNhuan);
    }

    // Ca 11: Pha2 exit + DaDoiDungLo — TB hai giá, dùng ±1% (không còn nhánh DungLo)

    [Fact]
    public async Task Pha3_TP1RoiQuayVeGiaVao_Trong1PhanTram_Ngang()
    {
        // GiaBanNua=101 (barely above entry), GiaThoatHet=100 (= entry, SL moved)
        // giaThoat = (101+100)/2 = 100.5 → pct = +0.5% < 1% → Ngang, TrangThai giữ DaKichHoat
        var db = NewDb();
        var entity = TaoEntity("HPG", KeHoachJson(100m, 95m, 110m));
        entity.GiaBanNua       = 101m;
        entity.ThoiGianBanNua  = DateTime.UtcNow.AddDays(-3);
        entity.DaDoiDungLo     = true;
        entity.GiaThoatHet     = 100m;
        entity.ThoiGianThoatHet = DateTime.UtcNow.AddDays(-1);
        entity.LyDoThoatHet    = "DungLo";
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", NenDongCua(100m));

        await TaoRunner(repo, db).RunAsync();

        Assert.Equal(100.5m, entity.GiaThoat);
        Assert.Equal("Ngang", entity.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai); // Ngang → không đổi
        Assert.Equal(0.5000m, entity.PhanTramLoiNhuan);
    }
}
