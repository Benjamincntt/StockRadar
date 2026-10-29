using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using Xunit.Abstractions;

namespace StockRadar.Tests.Notifications;

public sealed class GuiThongBaoTimeoutTests(ITestOutputHelper ketQua)
{
    /// <summary>Bounds a ten-second push hang to 2.5 seconds while sending Telegram exactly once.</summary>
    /// <returns>Completion of timed delivery assertions.</returns>
    [Fact]
    [Trait("Category", "ThoiGianThat")]
    public async Task DayTreoMuoiGiayKetThucTrongHaiGiayRuoi()
    {
        var thu = new BoThuGuiThongBao();
        thu.Day.KhiGui = async huy => { await Task.Delay(TimeSpan.FromSeconds(10), huy); return new(1, []); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui);
        var dongHo = Stopwatch.StartNew();
        await gui.GuiTatCaAsync();
        KiemTraThoiGian(dongHo, 2500);
        Assert.Single(thu.Telegram.Tin);
        Assert.Single(thu.Day.Tin);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Allows a five-second Telegram send to complete even after push has timed out.</summary>
    /// <returns>Completion of timed delivery assertions.</returns>
    [Fact]
    [Trait("Category", "ThoiGianThat")]
    public async Task TelegramNamGiayVanXongKhongBiHuy()
    {
        var thu = new BoThuGuiThongBao();
        var daXong = false;
        thu.Telegram.KhiGui = async huy => { await Task.Delay(TimeSpan.FromSeconds(5), huy); daXong = true; };
        thu.Day.KhiGui = async huy => { await Task.Delay(TimeSpan.FromSeconds(10), huy); return new(1, []); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui);
        var dongHo = Stopwatch.StartNew();
        await gui.GuiTatCaAsync();
        KiemTraThoiGian(dongHo, 6000);
        Assert.True(daXong);
        Assert.Single(thu.Telegram.Tin);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Skips hung push for the remaining batch and resets the circuit for the next batch.</summary>
    /// <returns>Completion of timed delivery assertions.</returns>
    [Fact]
    [Trait("Category", "ThoiGianThat")]
    public async Task NamTinDayTreoChiGoiMotLanVaDotSauThuLai()
    {
        var thu = new BoThuGuiThongBao();
        thu.Day.KhiGui = async huy => { await Task.Delay(TimeSpan.FromSeconds(10), huy); return new(1, []); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        var dongHo = Stopwatch.StartNew();
        await gui.GuiTatCaAsync();
        KiemTraThoiGian(dongHo, 3000);
        Assert.Single(thu.Day.Tin);
        Assert.Equal(5, thu.Telegram.Tin.Count);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
        thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(1, []));
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Equal(2, thu.Day.Tin.Count);
        Assert.Equal(6, thu.Telegram.Tin.Count);
        Assert.Equal(2, thu.Kho.SoLanDoc);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Opens the same batch circuit immediately on a connection failure.</summary>
    /// <returns>Completion of delivery assertions.</returns>
    [Fact]
    public async Task LoiKetNoiChiMotCanhBaoVaKhongThuLaiTrongDot()
    {
        var thu = new BoThuGuiThongBao();
        thu.Day.KhiGui = _ => throw new HttpRequestException("synthetic-only");
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        await gui.GuiTatCaAsync();
        Assert.Single(thu.Day.Tin);
        Assert.Equal(5, thu.Telegram.Tin.Count);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Tick cancellation discards pending messages without outage warnings or escaping exceptions.</summary>
    /// <returns>Completion of cancellation assertions.</returns>
    [Fact]
    public async Task HuyNhipBoTinConLaiVaXoaBoDem()
    {
        var thu = new BoThuGuiThongBao();
        var cho = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Telegram.KhiGui = huy => cho.Task.WaitAsync(huy);
        thu.Day.KhiGui = async huy => { daVao.TrySetResult(); await cho.Task.WaitAsync(huy); return new(1, []); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        using var huy = new CancellationTokenSource();
        var viec = gui.GuiTatCaAsync(huy.Token);
        await daVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        huy.Cancel();
        await viec;
        await gui.GuiTatCaAsync();
        Assert.Single(thu.Telegram.Tin);
        Assert.Single(thu.Day.Tin);
        Assert.Equal(0, thu.Log.Dem(LogLevel.Warning));
        Assert.Single(thu.Log.CacDong, dong => dong.NoiDung.Contains("canceled", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Pre-canceled ticks do not query devices or start either channel.</summary>
    /// <returns>Completion of cancellation assertions.</returns>
    [Fact]
    public async Task HuyTruocKhiGuiKhongGoiKenhNao()
    {
        var thu = new BoThuGuiThongBao();
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        await gui.GuiTatCaAsync(new CancellationToken(true));
        await gui.GuiTatCaAsync();
        Assert.Empty(thu.Telegram.Tin);
        Assert.Empty(thu.Day.Tin);
        Assert.Equal(0, thu.Kho.SoLanDoc);
    }

    /// <summary>A Telegram-specific timeout does not cancel push or the next message.</summary>
    /// <returns>Completion of timeout assertions.</returns>
    [Fact]
    public async Task QuaHanTelegramKhongNgatKenhDayHoacTinSau()
    {
        var dongHo = new DongHoGia();
        var thu = new BoThuGuiThongBao { DongHo = dongHo };
        thu.CauHinhTelegram.TimeoutGiay = 1;
        var cho = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tinHai = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var huyHai = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dayDaXong = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Day.KhiGui = _ =>
        {
            if (thu.Day.Tin.Count == 2) dayDaXong.TrySetResult();
            return Task.FromResult(new DayTinKetQua(1, []));
        };
        var soDaHuy = 0;
        thu.Telegram.KhiGui = huy =>
        {
            huy.Register(() => { if (Interlocked.Increment(ref soDaHuy) == 2) huyHai.TrySetResult(); });
            if (thu.Telegram.Tin.Count == 2) tinHai.TrySetResult();
            return cho.Task.WaitAsync(huy);
        };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 2);
        var viec = gui.GuiTatCaAsync();
        try
        {
            await dayDaXong.Task.WaitAsync(TimeSpan.FromSeconds(2));
            dongHo.Tien(TimeSpan.FromSeconds(1));
            await tinHai.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
            dongHo.Tien(TimeSpan.FromSeconds(1));
            await viec.WaitAsync(TimeSpan.FromMilliseconds(500));
            await huyHai.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(2, thu.Day.Tin.Count);
            Assert.Equal(2, thu.Telegram.Tin.Count);
            Assert.Equal(2, soDaHuy);
            Assert.Equal(2, thu.Log.Dem(LogLevel.Warning));
        }
        finally { cho.TrySetResult(); await viec; }
    }

    /// <summary>Applies the push budget to token lookup as well as network delivery.</summary>
    /// <returns>Completion of timed repository assertions.</returns>
    [Fact]
    public async Task DocTokenTreoVanGuiTelegramVaGiuHanCho()
    {
        var dongHo = new DongHoGia();
        var thu = new BoThuGuiThongBao { DongHo = dongHo };
        var daVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cho = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Kho.KhiDoc = huy => { daVao.TrySetResult(); return cho.Task.WaitAsync(huy); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        var viec = gui.GuiTatCaAsync();
        await daVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            dongHo.Tien(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(5, thu.Telegram.Tin.Count);
            Assert.Empty(thu.Day.Tin);
            Assert.Equal(1, thu.Kho.SoLanDoc);
            Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
        }
        finally { cho.TrySetResult(); await viec; }
    }

    /// <summary>Bounds waiting even when a transport ignores its cancellation token.</summary>
    /// <returns>Completion of timed transport assertions.</returns>
    [Fact]
    public async Task KenhBoQuaHuyVanKhongGiuNhipQuaHan()
    {
        var dongHo = new DongHoGia();
        var thu = new BoThuGuiThongBao { DongHo = dongHo };
        var treo = new TaskCompletionSource<DayTinKetQua>(TaskCreationOptions.RunContinuationsAsynchronously);
        var daVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Day.KhiGui = _ => { daVao.TrySetResult(); return treo.Task; };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        var viec = gui.GuiTatCaAsync();
        await daVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            dongHo.Tien(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Single(thu.Day.Tin);
            Assert.Equal(5, thu.Telegram.Tin.Count);
        }
        finally { treo.TrySetResult(new(0, [])); await viec; }
    }

    /// <summary>Reports elapsed time and asserts the owner's upper bound.</summary>
    /// <param name="dongHo">Running stopwatch for this dispatch.</param>
    /// <param name="gioiHanMs">Maximum allowed milliseconds.</param>
    private void KiemTraThoiGian(Stopwatch dongHo, int gioiHanMs)
    {
        dongHo.Stop();
        ketQua.WriteLine($"Elapsed={dongHo.Elapsed.TotalMilliseconds:F0} ms; limit={gioiHanMs} ms");
        Assert.True(dongHo.Elapsed.TotalMilliseconds <= gioiHanMs, $"Elapsed {dongHo.Elapsed.TotalMilliseconds:F0} ms exceeds {gioiHanMs} ms.");
    }
}
