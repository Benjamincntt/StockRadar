using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StockRadar.Api.Controllers;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

/// <summary>Covers the device registration contract of specs/013-mobile-push-notify/contracts/thiet-bi-api.md.</summary>
public sealed class ThietBiDangKyRouteTests
{
    private const string TokenMot = "token-gia-thiet-bi-00001";
    private const string TokenHai = "token-gia-thiet-bi-00002";
    private const string TokenCu = "token-gia-thiet-bi-cu-0003";

    /// <summary>Invalid input is rejected before any persistence work.</summary>
    /// <param name="token">Registration candidate under test.</param>
    /// <param name="nenTang">Platform candidate under test.</param>
    /// <returns>Completion of the validation assertion.</returns>
    [Theory]
    [InlineData("", "android")]
    [InlineData("   ", "android")]
    [InlineData("token-ngan", "android")]
    [InlineData("token co khoang trang trong do", "android")]
    [InlineData("token-gia-thiet-bi-00001", "ios")]
    public async Task DangKyDuLieuSaiBiTuChoi(string token, string nenTang)
    {
        await using var db = await TaoDbAsync();
        var thu = new BoThu();
        var dichVu = TaoDichVu(db, thu);

        var loi = await Assert.ThrowsAsync<ThietBiValidationException>(() =>
            dichVu.DangKyAsync(new DangKyThietBiRequest(token, nenTang, null)));

        Assert.False(string.IsNullOrWhiteSpace(loi.Message));
        Assert.Empty(await db.ThietBiNhanThongBao.ToListAsync());
    }

    [Fact]
    public async Task DangKyLapCungTokenChiGiuMotDongTheoUserCuoi()
    {
        await using var db = await TaoDbAsync();
        var thu = new BoThu();
        var dichVu = TaoDichVu(db, thu);

        await dichVu.DangKyAsync(new DangKyThietBiRequest(TokenMot, null, null));
        thu.NguoiDung.UserId = Guid.NewGuid();
        var ketQua = await dichVu.DangKyAsync(new DangKyThietBiRequest(TokenMot, "android", null));

        var dong = await db.ThietBiNhanThongBao.SingleAsync();
        Assert.Equal(TokenMot, dong.Token);
        Assert.Equal("android", dong.NenTang);
        Assert.Equal(thu.NguoiDung.UserId, dong.UserId);
        Assert.True(ketQua.DaDangKy);
    }

    [Fact]
    public async Task TokenCuCuaChinhUserBiXoaConTokenCuCuaUserKhacThiGiu()
    {
        await using var db = await TaoDbAsync();
        var nguoiKhac = Guid.NewGuid();
        var thu = new BoThu();
        db.ThietBiNhanThongBao.AddRange(
            new ThietBiNhanThongBaoEntity { Token = TokenCu, UserId = thu.NguoiDung.UserId, NenTang = "android" },
            new ThietBiNhanThongBaoEntity { Token = "token-gia-thiet-bi-cu-khac", UserId = nguoiKhac, NenTang = "android" });
        await db.SaveChangesAsync();
        var dichVu = TaoDichVu(db, thu);

        await dichVu.DangKyAsync(new DangKyThietBiRequest(TokenMot, "android", TokenCu));
        Assert.Null(await db.ThietBiNhanThongBao.SingleOrDefaultAsync(x => x.Token == TokenCu));

        await dichVu.DangKyAsync(new DangKyThietBiRequest(TokenHai, "android", "token-gia-thiet-bi-cu-khac"));
        Assert.NotNull(await db.ThietBiNhanThongBao.SingleOrDefaultAsync(x => x.Token == "token-gia-thiet-bi-cu-khac"));
        Assert.Equal(3, await db.ThietBiNhanThongBao.CountAsync());
    }

    [Fact]
    public async Task GoTokenLaKhongNemVaKhongXoaDongNao()
    {
        await using var db = await TaoDbAsync();
        db.ThietBiNhanThongBao.Add(new ThietBiNhanThongBaoEntity { Token = TokenMot, UserId = Guid.NewGuid(), NenTang = "android" });
        await db.SaveChangesAsync();
        var dichVu = TaoDichVu(db, new BoThu());

        await dichVu.GoAsync(new GoThietBiRequest("token-gia-khong-ton-tai-9"));

        Assert.Equal(1, await db.ThietBiNhanThongBao.CountAsync());
        await Assert.ThrowsAsync<ThietBiValidationException>(() => dichVu.GoAsync(new GoThietBiRequest(" ")));
    }

    [Fact]
    public async Task GuiThuKhiKenhTatBaoKhongGuiDuoc()
    {
        await using var db = await TaoDbAsync();
        var thu = new BoThu { KenhDangHoatDong = false };
        var dichVu = TaoDichVu(db, thu);

        var ketQua = await dichVu.GuiThuAsync();

        Assert.True(ketQua.KenhDayTat);
        Assert.Null(ketQua.KetQua);
        Assert.Empty(thu.KenhDay.Tin);
    }

    [Fact]
    public async Task GuiThuChiGuiChoMayCuaUserHienTai()
    {
        await using var db = await TaoDbAsync();
        var thu = new BoThu();
        db.ThietBiNhanThongBao.AddRange(
            new ThietBiNhanThongBaoEntity { Token = TokenMot, UserId = thu.NguoiDung.UserId, NenTang = "android" },
            new ThietBiNhanThongBaoEntity { Token = TokenHai, UserId = thu.NguoiDung.UserId, NenTang = "android" },
            new ThietBiNhanThongBaoEntity { Token = TokenCu, UserId = Guid.NewGuid(), NenTang = "android" });
        await db.SaveChangesAsync();
        var dichVu = TaoDichVu(db, thu);

        var ketQua = await dichVu.GuiThuAsync();

        Assert.False(ketQua.KenhDayTat);
        Assert.True(ketQua.KetQua!.GuiDuoc);
        Assert.Equal(2, ketQua.KetQua.SoThietBi);
        var lanGui = Assert.Single(thu.KenhDay.Tin);
        Assert.Equal(new[] { TokenMot, TokenHai }, lanGui.CacToken.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("v3-signal", lanGui.NoiDung.Kenh);
        Assert.Null(lanGui.NoiDung.Symbol);
    }

    [Fact]
    public async Task UserChuaCoMayNaoThiGuiThuBaoKhongGuiDuoc()
    {
        await using var db = await TaoDbAsync();
        var thu = new BoThu();
        var dichVu = TaoDichVu(db, thu);

        var ketQua = await dichVu.GuiThuAsync();

        Assert.False(ketQua.KenhDayTat);
        Assert.False(ketQua.KetQua!.GuiDuoc);
        Assert.Equal(0, ketQua.KetQua.SoThietBi);
        Assert.Empty(thu.KenhDay.Tin);
    }

    [Fact]
    public void ThietBiControllerBatBuocAuthorize()
    {
        var thuocTinh = typeof(ThietBiController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(thuocTinh);
        var route = typeof(ThietBiController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();
        Assert.Equal("api/v1/thiet-bi", route?.Template);
        var hanhDong = typeof(ThietBiController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(phuongThuc => phuongThuc.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()?.Template)
            .Where(tuyen => tuyen is not null)
            .OrderBy(tuyen => tuyen, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "dang-ky", "go", "gui-thu" }, hanhDong);
    }

    private static async Task<ApplicationDbContext> TaoDbAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ThietBiService TaoDichVu(ApplicationDbContext db, BoThu thu) =>
        new(new EfThietBiNhanThongBaoRepository(db), thu.KenhDay, thu.NguoiDung);

    private sealed class BoThu
    {
        internal NguoiDungGia NguoiDung { get; } = new();
        internal BoThuGuiThongBao.KenhDayGia KenhDay { get; } = new();

        internal bool KenhDangHoatDong
        {
            get => KenhDay.DangHoatDong;
            set => KenhDay.DangHoatDong = value;
        }
    }

    private sealed class NguoiDungGia : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? Email => "thiet-bi@test.local";
        public bool IsAuthenticated => true;
    }
}
