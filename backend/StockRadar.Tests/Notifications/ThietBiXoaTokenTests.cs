using Microsoft.EntityFrameworkCore;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StockRadar.Infrastructure.Notifications;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

public sealed class ThietBiXoaTokenTests
{
    /// <summary>Invalid-token results remove only rejected registrations from persistence and later sends.</summary>
    /// <returns>Completion of repository/dispatcher integration assertions.</returns>
    [Fact]
    public async Task TokenHongBiXoaNhungKhongNgatDot()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ThietBiNhanThongBao.AddRange(
            new ThietBiNhanThongBaoEntity { Token = "token-gia-hong-00001", UserId = Guid.NewGuid() },
            new ThietBiNhanThongBaoEntity { Token = "token-gia-khoe-00002", UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var thu = new BoThuGuiThongBao();
        thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(1,
            thu.Day.Tin.Count == 1 ? ["token-gia-hong-00001"] : []));
        var gui = thu.Tao(new EfThietBiNhanThongBaoRepository(db));
        BoThuGuiThongBao.Gom(gui, 5);
        await gui.GuiTatCaAsync();
        Assert.Equal(5, thu.Day.Tin.Count);
        Assert.Equal(5, thu.Telegram.Tin.Count);
        Assert.Equal(2, thu.Day.Tin[0].CacToken.Length);
        Assert.All(thu.Day.Tin.Skip(1), tin => Assert.Equal(new[] { "token-gia-khoe-00002" }, tin.CacToken));
        Assert.Equal("token-gia-khoe-00002", (await db.ThietBiNhanThongBao.SingleAsync()).Token);
        Assert.DoesNotContain(thu.Log.CacDong, dong => dong.Muc == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, 1, 4)]
    [InlineData(MessagingErrorCode.InvalidArgument, 2, 6)]
    public async Task LoiSdkThatChiXoaUnregisteredTrongDb(MessagingErrorCode maLoi, int soCon, int soGoi)
    {
        var loi = await KenhDayTinFcmTokenTests.TaoLoiSdkAsync(maLoi);
        var daGoi = new List<string>();
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () => (tin, _) =>
        {
            daGoi.Add(tin.Token);
            if (tin.Token.EndsWith("00001", StringComparison.Ordinal)) throw loi;
            return Task.FromResult("offline");
        });
        var thu = new BoThuGuiThongBao();
        await using var goc = TaoDb(thu, day);
        await using var phamVi = goc.CreateAsyncScope();
        var gui = phamVi.ServiceProvider.GetRequiredService<IGuiThongBao>();
        BoThuGuiThongBao.Gom(gui, 3);
        await gui.GuiTatCaAsync();
        var db = phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(soCon, await db.ThietBiNhanThongBao.CountAsync());
        Assert.Equal(soGoi, daGoi.Count);
        Assert.Equal(3, daGoi.Count(token => token.EndsWith("00002", StringComparison.Ordinal)));
        Assert.Equal(3, thu.Telegram.Tin.Count);
        Assert.Equal(0, thu.Log.Dem(Microsoft.Extensions.Logging.LogLevel.Warning));
    }

    [Fact]
    public async Task PayloadQuaLonGiuMoiDangKyVaTinSauCungDotVanGui()
    {
        var thu = new BoThuGuiThongBao();
        var soGui = 0;
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () => (_, _) =>
        {
            soGui++;
            return Task.FromResult("offline");
        });
        await using var goc = TaoDb(thu, day);
        await using var phamVi = goc.CreateAsyncScope();
        var gui = phamVi.ServiceProvider.GetRequiredService<IGuiThongBao>();
        gui.DatTinHeThong("HPO · xong", new string('x', 5000));
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Equal(2, await phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>().ThietBiNhanThongBao.CountAsync());
        Assert.Equal(2, soGui);
        Assert.Equal(2, thu.Telegram.Tin.Count);
        Assert.Equal(5000, thu.Telegram.Tin[0].NoiDung.Length);
        Assert.Equal(1, thu.Log.Dem(Microsoft.Extensions.Logging.LogLevel.Warning));
    }

    [Fact]
    public async Task CanhBaoLaiSauXoaUnregisteredVaDangKyLai()
    {
        var loi = await KenhDayTinFcmTokenTests.TaoLoiSdkAsync(MessagingErrorCode.Unregistered);
        var thuLog = new BoThuLogFcm();
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromException<string>(loi));
        var thu = new BoThuGuiThongBao();
        await using var goc = TaoDb(thu, day);
        await using var phamVi = goc.CreateAsyncScope();
        var kho = phamVi.ServiceProvider.GetRequiredService<IThietBiNhanThongBaoRepository>();
        await kho.XoaAsync("synthetic-device-00002");
        var maToken = "synthetic-device-00001";
        await day.GuiAsync([maToken], BoThuLogFcm.TinThu);
        await day.GuiAsync([maToken], BoThuLogFcm.TinThu);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(1, thuLog.Dem(LogLevel.Debug));
        var gui = phamVi.ServiceProvider.GetRequiredService<IGuiThongBao>();
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Empty(await kho.LayTokenCacMayAsync());
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(2, thuLog.Dem(LogLevel.Debug));
        await kho.DangKyAsync(Guid.NewGuid(), maToken, "android");
        Assert.Single(await kho.LayTokenCacMayAsync());
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Empty(await kho.LayTokenCacMayAsync());
        Assert.Equal(2, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(2, thuLog.Dem(LogLevel.Debug));
        Assert.Equal(2, thu.Telegram.Tin.Count);
        thuLog.KiemTraKhongLo(maToken, "loi-sdk-gia-khong-duoc-ghi");
    }

    [Fact]
    public async Task CanhBaoKhongLapLaiKhiXoaUnregisteredThatBai()
    {
        var loi = await KenhDayTinFcmTokenTests.TaoLoiSdkAsync(MessagingErrorCode.Unregistered);
        var thuLog = new BoThuLogFcm();
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromException<string>(loi));
        var thu = new BoThuGuiThongBao();
        thu.Kho.LoiXoa = true;
        var dichVu = thu.TaoDichVu();
        dichVu.AddSingleton<INguiTinDay>(day);
        await using var goc = dichVu.BuildServiceProvider();
        await using var phamVi = goc.CreateAsyncScope();
        var gui = phamVi.ServiceProvider.GetRequiredService<IGuiThongBao>();
        for (var lan = 0; lan < 2; lan++)
        {
            BoThuGuiThongBao.Gom(gui);
            await gui.GuiTatCaAsync();
        }
        Assert.Single(thu.Kho.CacToken);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(1, thuLog.Dem(LogLevel.Debug));
        Assert.Equal(2, thu.Log.Dem(LogLevel.Warning));
        thuLog.KiemTraKhongLo(thu.Kho.CacToken.ToArray());
    }

    [Fact]
    public async Task CanhBaoChiDatLaiSauXoaThanhCongDuDaQuaHan()
    {
        var loi = await KenhDayTinFcmTokenTests.TaoLoiSdkAsync(MessagingErrorCode.Unregistered);
        var thuLog = new BoThuLogFcm();
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromException<string>(loi));
        var dongHo = new DongHoGia();
        var thu = new BoThuGuiThongBao { DongHo = dongHo };
        var maToken = thu.Kho.CacToken.Single();
        var daVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choPhep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Kho.KhiXoa = _ => { daVao.TrySetResult(); return choPhep.Task; };
        var dichVu = thu.TaoDichVu();
        dichVu.AddSingleton<INguiTinDay>(day);
        await using var goc = dichVu.BuildServiceProvider();
        await using var phamVi = goc.CreateAsyncScope();
        var gui = phamVi.ServiceProvider.GetRequiredService<IGuiThongBao>();
        BoThuGuiThongBao.Gom(gui);
        var viec = gui.GuiTatCaAsync();
        try
        {
            await daVao.Task.WaitAsync(TimeSpan.FromSeconds(5));
            dongHo.Tien(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(thu.Kho.CacToken);
            await day.GuiAsync([maToken], BoThuLogFcm.TinThu);
            Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
            Assert.Equal(1, thuLog.Dem(LogLevel.Debug));
        }
        finally
        {
            choPhep.TrySetResult();
            await viec;
            await goc.GetRequiredService<PhamViDbThongBao>().ChayAsync(
                (_, _) => Task.FromResult(true), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Empty(thu.Kho.CacToken);
        thu.Kho.KhiXoa = null;
        thu.Kho.CacToken.Add(maToken);
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Empty(thu.Kho.CacToken);
        Assert.Equal(2, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(1, thuLog.Dem(LogLevel.Debug));
        thuLog.KiemTraKhongLo(maToken, "loi-sdk-gia-khong-duoc-ghi");
    }

    private static ServiceProvider TaoDb(BoThuGuiThongBao thu, INguiTinDay day)
    {
        var dichVu = thu.TaoDichVu();
        var tuyChon = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        dichVu.RemoveAll<ApplicationDbContext>();
        dichVu.AddScoped(_ => new ApplicationDbContext(tuyChon));
        dichVu.AddScoped<IThietBiNhanThongBaoRepository, EfThietBiNhanThongBaoRepository>();
        dichVu.AddSingleton(day);
        var goc = dichVu.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var phamVi = goc.CreateScope();
        var db = phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ThietBiNhanThongBao.AddRange(
            new ThietBiNhanThongBaoEntity { Token = "synthetic-device-00001", UserId = Guid.NewGuid() },
            new ThietBiNhanThongBaoEntity { Token = "synthetic-device-00002", UserId = Guid.NewGuid() });
        db.SaveChanges();
        return goc;
    }

    /// <summary>A cleanup failure still removes invalid tokens from the batch cache and preserves delivery.</summary>
    /// <returns>Completion of cleanup-failure assertions.</returns>
    [Fact]
    public async Task LoiLuuXoaKhongNgatTinConLai()
    {
        var thu = new BoThuGuiThongBao();
        thu.Kho.CacToken.Add("token-gia-hong-00002");
        thu.Kho.LoiXoa = true;
        thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(1,
            thu.Day.Tin.Count == 1 ? ["token-gia-hong-00002"] : []));
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 3);
        await gui.GuiTatCaAsync();
        Assert.Equal(3, thu.Day.Tin.Count);
        Assert.Equal(3, thu.Telegram.Tin.Count);
        Assert.All(thu.Day.Tin.Skip(1), tin => Assert.Single(tin.CacToken));
        Assert.All(thu.Log.CacDong, dong =>
        {
            Assert.Null(dong.Loi);
            Assert.DoesNotContain("synthetic-sensitive-cleanup-error", dong.NoiDung);
        });
    }
}
