using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class KenhDayTinFcmDisabledTests
{
    /// <summary>Verifies disabled and missing configurations warn only on the first empty send.</summary>
    /// <param name="bat">Whether push is enabled.</param>
    /// <param name="duongDan">An empty, whitespace, or nonexistent synthetic path.</param>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    [InlineData(true, "Z:\\khong-ton-tai-fcm-31f4b873\\gia.json")]
    public async Task ThieuCauHinhChiCanhBaoLanGuiDau(bool bat, string duongDan)
    {
        var thuLog = new BoThuLogFcm();
        var cauHinh = Options.Create(new PushNotifyOptions
        {
            Enabled = bat,
            Firebase = new CauHinhFirebase { CredentialPath = duongDan }
        });
        using var kenh = new KenhDayTinFcm(cauHinh, thuLog);
        Assert.Empty(thuLog.CacDong);
        Assert.False(kenh.DangHoatDong);
        Assert.False(kenh.DangHoatDong);
        Assert.Empty(thuLog.CacDong);

        for (var lan = 0; lan < 3; lan++)
        {
            var ketQua = await kenh.GuiAsync([], BoThuLogFcm.TinThu);
            Assert.Equal(0, ketQua.SoDaGui);
            Assert.Empty(ketQua.TokenHong);
            Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
            Assert.Equal(lan, thuLog.Dem(LogLevel.Debug));
        }
        if (!string.IsNullOrWhiteSpace(duongDan))
            thuLog.KiemTraKhongLo(duongDan);
    }

    /// <summary>Ensures disabling push prevents even an injected initializer from running.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task TatCauHinhKhongKhoiTaoDuCoDuongDanTonTai()
    {
        var cauHinh = BoThuLogFcm.CauHinhGia();
        cauHinh.Value.Enabled = false;
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(cauHinh, thuLog, () =>
        {
            soKhoiTao++;
            throw new InvalidOperationException("Synthetic initialization must not run.");
        });

        Assert.False(kenh.DangHoatDong);
        var ketQua = await kenh.GuiAsync(["thiet-bi-gia-khong-gui"], BoThuLogFcm.TinThu);
        Assert.Equal(0, soKhoiTao);
        Assert.Equal(0, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
    }

    /// <summary>Verifies concurrent missing-configuration sends emit exactly one warning.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task GuiDongThoiThieuCauHinhChiMotCanhBao()
    {
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(Options.Create(new PushNotifyOptions()), thuLog);
        var ketQua = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            kenh.GuiAsync([], BoThuLogFcm.TinThu))));

        Assert.All(ketQua, tin => Assert.Equal(0, tin.SoDaGui));
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(31, thuLog.Dem(LogLevel.Debug));
    }

    /// <summary>Verifies construction and repeated status queries do not initialize or log.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task ChiKhoiTaoLanGuiDauVaTaiSuDung()
    {
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        var soGui = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            Interlocked.Increment(ref soKhoiTao);
            return (_, _) =>
            {
                Interlocked.Increment(ref soGui);
                return Task.FromResult("ma-tin-gia");
            };
        });
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(thuLog.CacDong);
        Assert.True(kenh.DangHoatDong);
        Assert.True(kenh.DangHoatDong);
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(thuLog.CacDong);

        await kenh.GuiAsync([], BoThuLogFcm.TinThu);
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(0, soGui);
        var ketQua = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            kenh.GuiAsync(["thiet-bi-gia-khoe"], BoThuLogFcm.TinThu))));
        Assert.All(ketQua, tin => Assert.Equal(1, tin.SoDaGui));
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(16, soGui);
        Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
    }

    /// <summary>Verifies racing first sends share exactly one lazy initialization.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task GuiDauDongThoiChiKhoiTaoMotLan()
    {
        var soKhoiTao = 0;
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            Interlocked.Increment(ref soKhoiTao);
            return (_, _) => Task.FromResult("ma-tin-gia");
        });
        var batDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cacLanGui = Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
        {
            await batDau.Task;
            return await kenh.GuiAsync(["thiet-bi-gia"], BoThuLogFcm.TinThu);
        })).ToArray();
        Assert.Equal(0, soKhoiTao);
        batDau.SetResult();
        var ketQua = await Task.WhenAll(cacLanGui);
        Assert.Equal(1, soKhoiTao);
        Assert.All(ketQua, tin => Assert.Equal(1, tin.SoDaGui));
        Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
    }

    /// <summary>Caches malformed or failed initialization and never logs exception details or paths.</summary>
    /// <param name="saiDinhDang">Whether initialization fails because of malformed input.</param>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KhoiTaoLoiChiThuMotLanVaCanhBaoMotLan(bool saiDinhDang)
    {
        const string dauVetGia = "du-lieu-bi-mat-gia-khong-duoc-ghi";
        var thuLog = new BoThuLogFcm();
        var cauHinh = BoThuLogFcm.CauHinhGia();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(cauHinh, thuLog, () =>
        {
            Interlocked.Increment(ref soKhoiTao);
            throw saiDinhDang ? new FormatException(dauVetGia) : new InvalidOperationException(dauVetGia);
        });
        Assert.True(kenh.DangHoatDong);
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(thuLog.CacDong);

        var dau = await kenh.GuiAsync([], BoThuLogFcm.TinThu);
        Assert.Equal(0, dau.SoDaGui);
        Assert.Empty(dau.TokenHong);
        Assert.False(kenh.DangHoatDong);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(0, thuLog.Dem(LogLevel.Debug));
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            kenh.GuiAsync([], BoThuLogFcm.TinThu))));
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(16, thuLog.Dem(LogLevel.Debug));
        thuLog.KiemTraKhongLo(dauVetGia, cauHinh.Value.Firebase.CredentialPath);
    }

    /// <summary>Ensures disposal is idempotent and does not initialize an unused app.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task HuyKenhChuaDungKhongKhoiTaoVaKhongGuiSauHuy()
    {
        var soKhoiTao = 0;
        var thuLog = new BoThuLogFcm();
        var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            soKhoiTao++;
            return (_, _) => Task.FromResult("ma-tin-gia");
        });
        kenh.Dispose();
        kenh.Dispose();
        Assert.False(kenh.DangHoatDong);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(thuLog.CacDong);
    }
}
