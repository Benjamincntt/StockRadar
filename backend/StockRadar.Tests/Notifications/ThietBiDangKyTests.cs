using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Migrations;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

/// <summary>Verifies device persistence and schema contracts without external database access.</summary>
public sealed class ThietBiDangKyTests
{
    private static readonly DateTime NgayDangKyCu = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime NgayCapNhatCu = NgayDangKyCu.AddDays(1);

    /// <summary>Creates an isolated in-memory context for repository behavior tests.</summary>
    /// <returns>A context with a unique in-memory database.</returns>
    private static ApplicationDbContext TaoBoNho() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Creates a SQL Server model context with no configured connection.</summary>
    /// <param name="chanKetNoi">Guard rejecting any attempt to create a database connection.</param>
    /// <returns>A context usable only for model inspection.</returns>
    private static ApplicationDbContext TaoMoHinhSqlServer(ChanKetNoi chanKetNoi)
    {
        var tuyChon = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer()
            .AddInterceptors(chanKetNoi)
            .Options;
        var quanHe = Assert.Single(tuyChon.Extensions.OfType<RelationalOptionsExtension>());
        Assert.Null(quanHe.ConnectionString);
        Assert.Null(quanHe.Connection);
        return new ApplicationDbContext(tuyChon);
    }

    /// <summary>Creates a historical registration for deterministic timestamp assertions.</summary>
    /// <param name="ma">Stable row identifier.</param>
    /// <param name="maNguoiDung">Registration owner.</param>
    /// <param name="maDangKy">Synthetic registration value.</param>
    /// <returns>An Android registration with historical UTC timestamps.</returns>
    private static ThietBiNhanThongBaoEntity TaoMay(long ma, Guid maNguoiDung, string maDangKy) => new()
    {
        Id = ma,
        UserId = maNguoiDung,
        Token = maDangKy,
        NenTang = "android",
        NgayDangKy = NgayDangKyCu,
        LanCapNhatGanNhat = NgayCapNhatCu
    };

    /// <summary>Seeds registrations directly so read and delete tests do not depend on registration.</summary>
    /// <param name="coSoDuLieu">Isolated in-memory context.</param>
    /// <param name="cacMay">Synthetic registrations to persist.</param>
    /// <returns>Completion after saving and clearing tracked entities.</returns>
    private static async Task GhiMauAsync(ApplicationDbContext coSoDuLieu,
        params ThietBiNhanThongBaoEntity[] cacMay)
    {
        coSoDuLieu.ThietBiNhanThongBao.AddRange(cacMay);
        await coSoDuLieu.SaveChangesAsync();
        coSoDuLieu.ChangeTracker.Clear();
    }

    /// <summary>Checks that an update preserves identity and registration time while refreshing UTC time.</summary>
    /// <param name="may">Persisted registration.</param>
    /// <param name="ma">Expected unchanged row identifier.</param>
    /// <param name="maNguoiDung">Expected current owner.</param>
    /// <param name="truoc">UTC time immediately before the operation.</param>
    private static void KiemTraCapNhat(ThietBiNhanThongBaoEntity may, long ma,
        Guid maNguoiDung, DateTime truoc)
    {
        Assert.Equal(ma, may.Id);
        Assert.Equal(maNguoiDung, may.UserId);
        Assert.Equal(NgayDangKyCu, may.NgayDangKy);
        Assert.Equal(DateTimeKind.Utc, may.NgayDangKy.Kind);
        Assert.Equal(DateTimeKind.Utc, may.LanCapNhatGanNhat.Kind);
        Assert.True(may.LanCapNhatGanNhat > NgayCapNhatCu);
        Assert.InRange(may.LanCapNhatGanNhat, truoc, DateTime.UtcNow);
    }

    /// <summary>Checks an untouched registration, including owner, platform, and timestamps.</summary>
    /// <param name="mongDoi">Registration before the operation.</param>
    /// <param name="thucTe">Registration after the operation.</param>
    private static void KiemTraKhongDoi(ThietBiNhanThongBaoEntity mongDoi, ThietBiNhanThongBaoEntity thucTe)
    {
        Assert.Equal(mongDoi.Id, thucTe.Id);
        Assert.Equal(mongDoi.UserId, thucTe.UserId);
        Assert.Equal(mongDoi.Token, thucTe.Token);
        Assert.Equal(mongDoi.NenTang, thucTe.NenTang);
        Assert.Equal(mongDoi.NgayDangKy, thucTe.NgayDangKy);
        Assert.Equal(mongDoi.LanCapNhatGanNhat, thucTe.LanCapNhatGanNhat);
    }

    /// <summary>New registrations trim the token and persist the supplied platform and UTC timestamps.</summary>
    /// <param name="nenTang">Platform to store unchanged.</param>
    /// <returns>Completion after verifying the persisted row.</returns>
    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyMoi_CatKhoangTrang_LuuNenTangVaUtc(string nenTang)
    {
        await using var coSoDuLieu = TaoBoNho();
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        var maNguoiDung = Guid.NewGuid();
        var truoc = DateTime.UtcNow;

        await kho.DangKyAsync(maNguoiDung, " \tma-thu-moi\r\n", nenTang);

        coSoDuLieu.ChangeTracker.Clear();
        var may = Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync());
        Assert.True(may.Id > 0);
        Assert.Equal(maNguoiDung, may.UserId);
        Assert.Equal("ma-thu-moi", may.Token);
        Assert.Equal(nenTang, may.NenTang);
        Assert.Equal(DateTimeKind.Utc, may.NgayDangKy.Kind);
        Assert.Equal(DateTimeKind.Utc, may.LanCapNhatGanNhat.Kind);
        Assert.InRange(may.NgayDangKy, truoc, DateTime.UtcNow);
        Assert.Equal(may.NgayDangKy, may.LanCapNhatGanNhat);
    }

    /// <summary>Registering the same normalized token twice keeps one row and its original registration date.</summary>
    /// <param name="maDangKyLanHai">Second registration value with optional whitespace.</param>
    /// <returns>Completion after checking the second persisted registration.</returns>
    [Theory]
    [InlineData("ma-thu-lap")]
    [InlineData(" \tma-thu-lap\r\n")]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyLapLai_GiuMotDongVaNgayDangKy(string maDangKyLanHai)
    {
        await using var coSoDuLieu = TaoBoNho();
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        var maNguoiDung = Guid.NewGuid();
        await kho.DangKyAsync(maNguoiDung, "ma-thu-lap", "android");
        var lanDau = await coSoDuLieu.ThietBiNhanThongBao.SingleAsync();
        var ma = lanDau.Id;
        lanDau.NgayDangKy = NgayDangKyCu;
        lanDau.LanCapNhatGanNhat = NgayCapNhatCu;
        await coSoDuLieu.SaveChangesAsync();
        coSoDuLieu.ChangeTracker.Clear();
        var truoc = DateTime.UtcNow;

        await kho.DangKyAsync(maNguoiDung, maDangKyLanHai, "ios");

        coSoDuLieu.ChangeTracker.Clear();
        var may = Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync());
        KiemTraCapNhat(may, ma, maNguoiDung, truoc);
        Assert.Equal("ma-thu-lap", may.Token);
        Assert.Equal("ios", may.NenTang);
    }

    /// <summary>Moving a token between users updates its owner instead of inserting another row.</summary>
    /// <returns>Completion after checking the new owner, platform, and preserved registration.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyChuyenChu_GiuMotDongVaNgayDangKy()
    {
        await using var coSoDuLieu = TaoBoNho();
        var chuCu = Guid.NewGuid();
        var chuMoi = Guid.NewGuid();
        await GhiMauAsync(coSoDuLieu, TaoMay(101, chuCu, "ma-thu-chuyen"));
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        var truoc = DateTime.UtcNow;

        await kho.DangKyAsync(chuMoi, " ma-thu-chuyen ", "ios");

        coSoDuLieu.ChangeTracker.Clear();
        var may = Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync());
        KiemTraCapNhat(may, 101, chuMoi, truoc);
        Assert.Equal("ma-thu-chuyen", may.Token);
        Assert.Equal("ios", may.NenTang);
        Assert.Empty(await kho.LayTokenTheoUserAsync(chuCu));
        Assert.Equal(new[] { "ma-thu-chuyen" }, await kho.LayTokenTheoUserAsync(chuMoi));
    }

    /// <summary>Rotation removes only a distinct old token owned by the current user.</summary>
    /// <param name="cungChu">Whether the old registration belongs to the current user.</param>
    /// <param name="daCoMayMoi">Whether the new token already exists under another owner.</param>
    /// <returns>Completion after checking all affected and unrelated rows.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyDoiMaCu_ChiXoaMaKhacCuaNguoiHienTai(bool cungChu, bool daCoMayMoi)
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        var nguoiKhac = Guid.NewGuid();
        var mayCu = TaoMay(101, cungChu ? maNguoiDung : nguoiKhac, "ma-thu-cu");
        var mayKhacCungChu = TaoMay(102, maNguoiDung, "ma-thu-giu-cung-chu");
        var mayKhacChu = TaoMay(103, nguoiKhac, "ma-thu-giu-khac-chu");
        await GhiMauAsync(coSoDuLieu, mayCu, mayKhacCungChu, mayKhacChu);
        if (daCoMayMoi)
            await GhiMauAsync(coSoDuLieu, TaoMay(104, nguoiKhac, "ma-thu-moi"));
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        var truoc = DateTime.UtcNow;

        await kho.DangKyAsync(maNguoiDung, " ma-thu-moi ", "ios", " \tma-thu-cu\r\n");

        coSoDuLieu.ChangeTracker.Clear();
        var cacMay = await coSoDuLieu.ThietBiNhanThongBao.ToListAsync();
        Assert.Equal(cungChu ? 3 : 4, cacMay.Count);
        var mayMoi = Assert.Single(cacMay, may => may.Token == "ma-thu-moi");
        Assert.Equal(maNguoiDung, mayMoi.UserId);
        Assert.Equal("ios", mayMoi.NenTang);
        if (daCoMayMoi)
            KiemTraCapNhat(mayMoi, 104, maNguoiDung, truoc);
        if (cungChu)
            Assert.DoesNotContain(cacMay, may => may.Token == "ma-thu-cu");
        else
            KiemTraKhongDoi(mayCu, Assert.Single(cacMay, may => may.Id == 101));
        KiemTraKhongDoi(mayKhacCungChu, Assert.Single(cacMay, may => may.Id == 102));
        KiemTraKhongDoi(mayKhacChu, Assert.Single(cacMay, may => may.Id == 103));
    }

    /// <summary>Equal normalized old and new tokens never delete and recreate the registration.</summary>
    /// <param name="maMoi">New synthetic token.</param>
    /// <param name="maCu">Equivalent old synthetic token.</param>
    /// <param name="doiChu">Whether ownership also changes during the update.</param>
    /// <returns>Completion after verifying stable identity and registration time.</returns>
    [Theory]
    [InlineData("ma-thu-trung", "ma-thu-trung", false)]
    [InlineData(" ma-thu-trung ", "\tma-thu-trung\r\n", false)]
    [InlineData(" ma-thu-trung ", "ma-thu-trung", true)]
    [InlineData("ma-thu-trung", " ma-thu-trung ", true)]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyMaCuTrungMaMoi_KhongXoaTaoLai(string maMoi, string maCu, bool doiChu)
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        await GhiMauAsync(coSoDuLieu, TaoMay(101, doiChu ? Guid.NewGuid() : maNguoiDung, "ma-thu-trung"));
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        var truoc = DateTime.UtcNow;

        await kho.DangKyAsync(maNguoiDung, maMoi, "ios", maCu);

        coSoDuLieu.ChangeTracker.Clear();
        var may = Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync());
        KiemTraCapNhat(may, 101, maNguoiDung, truoc);
        Assert.Equal("ma-thu-trung", may.Token);
        Assert.Equal("ios", may.NenTang);
    }

    /// <summary>Absent, blank, and unknown old tokens leave existing registrations untouched.</summary>
    /// <param name="maCu">Optional old registration value.</param>
    /// <returns>Completion after verifying the existing row and the new registration.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData(" ma-thu-khong-co ")]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DangKyKhongCoMaCu_KhongXoaMayKhac(string? maCu)
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        var mayGiuLai = TaoMay(101, maNguoiDung, "ma-thu-giu");
        await GhiMauAsync(coSoDuLieu, mayGiuLai);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        await kho.DangKyAsync(maNguoiDung, "ma-thu-moi", "android", maCu);

        coSoDuLieu.ChangeTracker.Clear();
        var cacMay = await coSoDuLieu.ThietBiNhanThongBao.ToListAsync();
        Assert.Equal(2, cacMay.Count);
        KiemTraKhongDoi(mayGiuLai, Assert.Single(cacMay, may => may.Id == 101));
        Assert.Single(cacMay, may => may.Token == "ma-thu-moi" && may.UserId == maNguoiDung);
    }

    /// <summary>User queries include all of that user's devices and none from another user.</summary>
    /// <returns>Completion after checking populated and unknown-user queries.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task LayTheoNguoiDung_ChiTraMayCuaNguoiDo()
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        await GhiMauAsync(coSoDuLieu, TaoMay(101, maNguoiDung, "ma-thu-a"),
            TaoMay(102, maNguoiDung, "ma-thu-b"), TaoMay(103, Guid.NewGuid(), "ma-thu-c"));
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        var cacMa = await kho.LayTokenTheoUserAsync(maNguoiDung);

        Assert.Equal(new[] { "ma-thu-a", "ma-thu-b" }, cacMa.OrderBy(ma => ma));
        Assert.Empty(await kho.LayTokenTheoUserAsync(Guid.NewGuid()));
        Assert.Empty(coSoDuLieu.ChangeTracker.Entries());
    }

    /// <summary>Broadcast queries return every registration across multiple users without tracking entities.</summary>
    /// <returns>Completion after checking all stored synthetic tokens.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task LayTatCa_TraMayCuaMoiNguoiDung()
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        await GhiMauAsync(coSoDuLieu, TaoMay(101, maNguoiDung, "ma-thu-a"),
            TaoMay(102, maNguoiDung, "ma-thu-b"), TaoMay(103, Guid.NewGuid(), "ma-thu-c"));
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        var cacMa = await kho.LayTokenCacMayAsync();

        Assert.Equal(new[] { "ma-thu-a", "ma-thu-b", "ma-thu-c" }, cacMa.OrderBy(ma => ma));
        Assert.Empty(coSoDuLieu.ChangeTracker.Entries());
    }

    /// <summary>Both query methods return empty collections for an empty store.</summary>
    /// <returns>Completion after checking both queries.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task LayDuLieuRong_TraDanhSachRong()
    {
        await using var coSoDuLieu = TaoBoNho();
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        Assert.Empty(await kho.LayTokenTheoUserAsync(Guid.NewGuid()));
        Assert.Empty(await kho.LayTokenCacMayAsync());
    }

    /// <summary>Deletion needs no owner parameter, trims the token, and is safe to repeat.</summary>
    /// <param name="maXoa">Registration value with optional surrounding whitespace.</param>
    /// <returns>Completion after checking repeated deletion and untouched rows.</returns>
    [Theory]
    [InlineData("ma-thu-xoa")]
    [InlineData(" \tma-thu-xoa\r\n")]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task XoaKhongCanChu_LapLaiKhongLoi(string maXoa)
    {
        await using var coSoDuLieu = TaoBoNho();
        var chuCu = Guid.NewGuid();
        var mayGiuLai = TaoMay(102, chuCu, "ma-thu-giu");
        var mayKhacChu = TaoMay(103, Guid.NewGuid(), "ma-thu-khac-chu");
        await GhiMauAsync(coSoDuLieu, TaoMay(101, chuCu, "ma-thu-xoa"), mayGiuLai, mayKhacChu);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        await kho.XoaAsync(maXoa);
        coSoDuLieu.ChangeTracker.Clear();
        Assert.False(await coSoDuLieu.ThietBiNhanThongBao.AnyAsync(may => may.Token == "ma-thu-xoa"));
        await kho.XoaAsync(maXoa);

        coSoDuLieu.ChangeTracker.Clear();
        var cacMay = await coSoDuLieu.ThietBiNhanThongBao.ToListAsync();
        Assert.Equal(2, cacMay.Count);
        KiemTraKhongDoi(mayGiuLai, Assert.Single(cacMay, may => may.Id == 102));
        KiemTraKhongDoi(mayKhacChu, Assert.Single(cacMay, may => may.Id == 103));
    }

    /// <summary>Deleting an unknown registration cannot remove any existing device.</summary>
    /// <returns>Completion after checking the unchanged store.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task XoaMaKhongCo_KhongThayDoiDuLieu()
    {
        await using var coSoDuLieu = TaoBoNho();
        var mayGiuLai = TaoMay(101, Guid.NewGuid(), "ma-thu-giu");
        await GhiMauAsync(coSoDuLieu, mayGiuLai);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        await kho.XoaAsync("ma-thu-khong-co");

        coSoDuLieu.ChangeTracker.Clear();
        KiemTraKhongDoi(mayGiuLai, Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync()));
    }

    /// <summary>Batch deletion removes only supplied tokens, tolerating duplicates and missing registrations.</summary>
    /// <returns>Completion after checking repeated deletion across owners and preserved neighbors.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task XoaNhieu_ChiXoaMaDuocChiDinh()
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        var nguoiKhac = Guid.NewGuid();
        var mayGiuLai = TaoMay(103, maNguoiDung, "ma-thu-a-khac");
        var mayKhacChu = TaoMay(104, nguoiKhac, "ma-thu-giu");
        await GhiMauAsync(coSoDuLieu, TaoMay(101, maNguoiDung, "ma-thu-a"),
            TaoMay(102, nguoiKhac, "ma-thu-b"), mayGiuLai, mayKhacChu);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        string[] cacMaXoa = ["ma-thu-a", "ma-thu-b", "ma-thu-a", "ma-thu-khong-co"];

        await kho.XoaCacTokenAsync(cacMaXoa);
        coSoDuLieu.ChangeTracker.Clear();
        await kho.XoaCacTokenAsync(cacMaXoa);

        coSoDuLieu.ChangeTracker.Clear();
        var cacMay = await coSoDuLieu.ThietBiNhanThongBao.ToListAsync();
        Assert.Equal(2, cacMay.Count);
        KiemTraKhongDoi(mayGiuLai, Assert.Single(cacMay, may => may.Id == 103));
        KiemTraKhongDoi(mayKhacChu, Assert.Single(cacMay, may => may.Id == 104));
    }

    /// <summary>An empty invalid-token batch cannot delete registrations.</summary>
    /// <returns>Completion after checking both existing owners' registrations.</returns>
    [Fact]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task XoaNhieuDanhSachRong_KhongXoaMayNao()
    {
        await using var coSoDuLieu = TaoBoNho();
        var mayThuNhat = TaoMay(101, Guid.NewGuid(), "ma-thu-a");
        var mayThuHai = TaoMay(102, Guid.NewGuid(), "ma-thu-b");
        await GhiMauAsync(coSoDuLieu, mayThuNhat, mayThuHai);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);

        await kho.XoaCacTokenAsync([]);

        coSoDuLieu.ChangeTracker.Clear();
        var cacMay = await coSoDuLieu.ThietBiNhanThongBao.ToListAsync();
        Assert.Equal(2, cacMay.Count);
        KiemTraKhongDoi(mayThuNhat, Assert.Single(cacMay, may => may.Id == 101));
        KiemTraKhongDoi(mayThuHai, Assert.Single(cacMay, may => may.Id == 102));
    }

    /// <summary>All repository methods honor a canceled database operation without persisting changes.</summary>
    /// <param name="thaoTac">Repository operation selected for cancellation.</param>
    /// <returns>Completion after checking cancellation and the unchanged row.</returns>
    [Theory]
    [InlineData("dang-ky")]
    [InlineData("xoa")]
    [InlineData("xoa-nhieu")]
    [InlineData("lay-nguoi-dung")]
    [InlineData("lay-tat-ca")]
    [Trait("Nhom", "KhoLuuTru")]
    public async Task DaHuy_KhongGhiDuLieu(string thaoTac)
    {
        await using var coSoDuLieu = TaoBoNho();
        var maNguoiDung = Guid.NewGuid();
        var mayGiuLai = TaoMay(101, maNguoiDung, "ma-thu-giu");
        await GhiMauAsync(coSoDuLieu, mayGiuLai);
        IThietBiNhanThongBaoRepository kho = new EfThietBiNhanThongBaoRepository(coSoDuLieu);
        using var nguonHuy = new CancellationTokenSource();
        nguonHuy.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => thaoTac switch
        {
            "dang-ky" => kho.DangKyAsync(maNguoiDung, "ma-thu-moi", "ios", "ma-thu-giu", nguonHuy.Token),
            "xoa" => kho.XoaAsync("ma-thu-giu", nguonHuy.Token),
            "xoa-nhieu" => kho.XoaCacTokenAsync(["ma-thu-giu"], nguonHuy.Token),
            "lay-nguoi-dung" => kho.LayTokenTheoUserAsync(maNguoiDung, nguonHuy.Token),
            "lay-tat-ca" => kho.LayTokenCacMayAsync(nguonHuy.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(thaoTac))
        });

        coSoDuLieu.ChangeTracker.Clear();
        KiemTraKhongDoi(mayGiuLai, Assert.Single(await coSoDuLieu.ThietBiNhanThongBao.ToListAsync()));
    }

    /// <summary>A newly constructed entity defaults to the Android platform.</summary>
    [Fact]
    [Trait("Nhom", "MoHinh")]
    public void ThucTheMacDinh_NenTangAndroid()
    {
        Assert.Equal("android", new ThietBiNhanThongBaoEntity().NenTang);
    }

    /// <summary>SQL Server metadata defines required columns, indexes, identity, and cascading ownership.</summary>
    [Fact]
    [Trait("Nhom", "MoHinh")]
    public void MoHinhSqlServer_DungCotChiMucVaKhoaNgoai()
    {
        var chanKetNoi = new ChanKetNoi();
        using var coSoDuLieu = TaoMoHinhSqlServer(chanKetNoi);
        var thucThe = coSoDuLieu.Model.FindEntityType(typeof(ThietBiNhanThongBaoEntity));
        Assert.NotNull(thucThe);
        Assert.Equal("ThietBiNhanThongBao", thucThe.GetTableName());
        Assert.Equal(nameof(ThietBiNhanThongBaoEntity.Id), Assert.Single(thucThe.FindPrimaryKey()!.Properties).Name);
        var cacCot = thucThe.GetProperties().ToDictionary(cot => cot.Name);
        Assert.Equal(6, cacCot.Count);
        Assert.All(cacCot.Values, cot => Assert.False(cot.IsNullable));
        Assert.Equal(typeof(long), cacCot["Id"].ClrType);
        Assert.Equal("bigint", cacCot["Id"].GetColumnType());
        Assert.Equal(ValueGenerated.OnAdd, cacCot["Id"].ValueGenerated);
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, cacCot["Id"].GetValueGenerationStrategy());
        Assert.Equal(typeof(Guid), cacCot["UserId"].ClrType);
        Assert.Equal("uniqueidentifier", cacCot["UserId"].GetColumnType());
        Assert.Equal(typeof(string), cacCot["Token"].ClrType);
        Assert.Equal(512, cacCot["Token"].GetMaxLength());
        Assert.Equal("nvarchar(512)", cacCot["Token"].GetColumnType());
        Assert.Equal(typeof(string), cacCot["NenTang"].ClrType);
        Assert.Equal(16, cacCot["NenTang"].GetMaxLength());
        Assert.Equal("nvarchar(16)", cacCot["NenTang"].GetColumnType());
        Assert.Equal("android", cacCot["NenTang"].GetDefaultValue());
        foreach (var tenCot in new[] { "NgayDangKy", "LanCapNhatGanNhat" })
        {
            Assert.Equal(typeof(DateTime), cacCot[tenCot].ClrType);
            Assert.Equal("datetime2", cacCot[tenCot].GetColumnType());
        }
        var cacChiMuc = thucThe.GetIndexes().ToList();
        Assert.Equal(2, cacChiMuc.Count);
        var chiMucMa = Assert.Single(cacChiMuc, chiMuc => chiMuc.Properties.Single().Name == "Token");
        Assert.True(chiMucMa.IsUnique);
        var chiMucChu = Assert.Single(cacChiMuc, chiMuc => chiMuc.Properties.Single().Name == "UserId");
        Assert.False(chiMucChu.IsUnique);
        var khoaNgoai = Assert.Single(thucThe.GetForeignKeys());
        Assert.Equal("UserId", Assert.Single(khoaNgoai.Properties).Name);
        Assert.Equal(typeof(UserEntity), khoaNgoai.PrincipalEntityType.ClrType);
        Assert.Equal("Users", khoaNgoai.PrincipalEntityType.GetTableName());
        Assert.Equal("Id", Assert.Single(khoaNgoai.PrincipalKey.Properties).Name);
        Assert.True(khoaNgoai.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, khoaNgoai.DeleteBehavior);
        Assert.Equal(0, chanKetNoi.SoLanTaoKetNoi);
    }

    /// <summary>The migration adds exactly one table and two indexes; rollback only drops that table.</summary>
    [Fact]
    [Trait("Nhom", "MoHinh")]
    public void DiTru_ChiTaoBangHaiChiMucVaXoaBangKhiLui()
    {
        var diTru = new AddThietBiNhanThongBao();
        Assert.Equal("20261029100754_AddThietBiNhanThongBao",
            Assert.Single(typeof(AddThietBiNhanThongBao).GetCustomAttributes(typeof(MigrationAttribute), false)
                .Cast<MigrationAttribute>()).Id);
        Assert.Equal(3, diTru.UpOperations.Count);
        var bang = Assert.IsType<CreateTableOperation>(diTru.UpOperations[0]);
        Assert.Equal("ThietBiNhanThongBao", bang.Name);
        Assert.Null(bang.Schema);
        var cacCot = bang.Columns.ToDictionary(cot => cot.Name);
        Assert.Equal(6, cacCot.Count);
        Assert.All(cacCot.Values, cot => Assert.False(cot.IsNullable));
        Assert.Equal(typeof(long), cacCot["Id"].ClrType);
        Assert.Equal("bigint", cacCot["Id"].ColumnType);
        Assert.Equal("1, 1", cacCot["Id"]["SqlServer:Identity"]);
        Assert.Equal(typeof(Guid), cacCot["UserId"].ClrType);
        Assert.Equal("uniqueidentifier", cacCot["UserId"].ColumnType);
        Assert.Equal(typeof(string), cacCot["Token"].ClrType);
        Assert.Equal(512, cacCot["Token"].MaxLength);
        Assert.Equal("nvarchar(512)", cacCot["Token"].ColumnType);
        Assert.Equal(typeof(string), cacCot["NenTang"].ClrType);
        Assert.Equal(16, cacCot["NenTang"].MaxLength);
        Assert.Equal("nvarchar(16)", cacCot["NenTang"].ColumnType);
        Assert.Equal("android", cacCot["NenTang"].DefaultValue);
        foreach (var tenCot in new[] { "NgayDangKy", "LanCapNhatGanNhat" })
        {
            Assert.Equal(typeof(DateTime), cacCot[tenCot].ClrType);
            Assert.Equal("datetime2", cacCot[tenCot].ColumnType);
        }
        Assert.NotNull(bang.PrimaryKey);
        Assert.Equal("Id", Assert.Single(bang.PrimaryKey.Columns));
        Assert.Empty(bang.UniqueConstraints);
        Assert.Empty(bang.CheckConstraints);
        var khoaNgoai = Assert.Single(bang.ForeignKeys);
        Assert.Equal(bang.Name, khoaNgoai.Table);
        Assert.Equal("UserId", Assert.Single(khoaNgoai.Columns));
        Assert.Equal("Users", khoaNgoai.PrincipalTable);
        Assert.Equal("Id", Assert.Single(khoaNgoai.PrincipalColumns!));
        Assert.Equal(ReferentialAction.Cascade, khoaNgoai.OnDelete);
        Assert.Equal(ReferentialAction.NoAction, khoaNgoai.OnUpdate);
        var cacChiMuc = diTru.UpOperations.Skip(1)
            .Select(thaoTac => Assert.IsType<CreateIndexOperation>(thaoTac)).ToList();
        Assert.All(cacChiMuc, chiMuc => Assert.Equal(bang.Name, chiMuc.Table));
        var chiMucMa = Assert.Single(cacChiMuc, chiMuc => chiMuc.Columns.Single() == "Token");
        Assert.Equal("IX_ThietBiNhanThongBao_Token", chiMucMa.Name);
        Assert.True(chiMucMa.IsUnique);
        Assert.Null(chiMucMa.Filter);
        var chiMucChu = Assert.Single(cacChiMuc, chiMuc => chiMuc.Columns.Single() == "UserId");
        Assert.Equal("IX_ThietBiNhanThongBao_UserId", chiMucChu.Name);
        Assert.False(chiMucChu.IsUnique);
        Assert.Null(chiMucChu.Filter);
        var xoaBang = Assert.IsType<DropTableOperation>(Assert.Single(diTru.DownOperations));
        Assert.Equal(bang.Name, xoaBang.Name);
        Assert.Null(xoaBang.Schema);
    }

    /// <summary>The checked-in SQL Server snapshot matches the current model without any database connection.</summary>
    [Fact]
    [Trait("Nhom", "MoHinh")]
    public void AnhChupMoHinh_KhongCoThayDoiChoVaKhongKetNoi()
    {
        var chanKetNoi = new ChanKetNoi();
        using var coSoDuLieu = TaoMoHinhSqlServer(chanKetNoi);

        Assert.False(coSoDuLieu.Database.HasPendingModelChanges());

        Assert.Equal(0, chanKetNoi.SoLanTaoKetNoi);
    }

    /// <summary>Rejects connection creation so metadata-only tests cannot reach a database.</summary>
    private sealed class ChanKetNoi : DbConnectionInterceptor
    {
        public int SoLanTaoKetNoi { get; private set; }

        /// <inheritdoc />
        public override InterceptionResult<DbConnection> ConnectionCreating(
            ConnectionCreatingEventData eventData, InterceptionResult<DbConnection> result)
        {
            SoLanTaoKetNoi++;
            throw new InvalidOperationException("Metadata tests must not create database connections.");
        }
    }
}
