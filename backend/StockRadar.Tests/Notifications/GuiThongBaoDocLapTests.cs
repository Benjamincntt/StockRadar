using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class GuiThongBaoDocLapTests
{
    /// <summary>Verifies either or both channel failures never escape or suppress the other attempt.</summary>
    /// <param name="loiTelegram">Whether Telegram fails synchronously.</param>
    /// <param name="loiDay">Whether push fails synchronously.</param>
    /// <returns>Completion of delivery assertions.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoiDocLapKhongNganKenhConLai(bool loiTelegram, bool loiDay)
    {
        var thu = new BoThuGuiThongBao();
        if (loiTelegram) thu.Telegram.KhiGui = _ => throw new HttpRequestException("synthetic-sensitive-error");
        if (loiDay) thu.Day.KhiGui = _ => throw new HttpRequestException("synthetic-sensitive-error");
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Single(thu.Telegram.Tin);
        Assert.Single(thu.Day.Tin);
        Assert.All(thu.Log.CacDong, dong =>
        {
            Assert.Null(dong.Loi);
            Assert.DoesNotContain("synthetic-sensitive-error", dong.NoiDung);
        });
    }

    /// <summary>Ensures a device-query failure cannot stop Telegram and opens only one batch warning.</summary>
    /// <returns>Completion of delivery assertions.</returns>
    [Fact]
    public async Task LoiDocMayVanGuiTatCaTelegram()
    {
        var thu = new BoThuGuiThongBao();
        thu.Kho.KhiDoc = _ => throw new InvalidOperationException("synthetic-sensitive-error");
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 5);
        await gui.GuiTatCaAsync();
        Assert.Equal(5, thu.Telegram.Tin.Count);
        Assert.Empty(thu.Day.Tin);
        Assert.Equal(1, thu.Kho.SoLanDoc);
        Assert.Equal(1, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Ensures a disabled real adapter warns on first dispatch without querying devices.</summary>
    /// <returns>Completion of delivery assertions.</returns>
    [Fact]
    public async Task KenhTatCanhBaoLanGuiDauKhongDocDb()
    {
        var thu = new BoThuGuiThongBao();
        var logDay = new BoThuLogFcm();
        using var day = new KenhDayTinFcm(Options.Create(new PushNotifyOptions()), logDay);
        var gui = thu.Tao(day: day);
        Assert.Empty(logDay.CacDong);
        BoThuGuiThongBao.Gom(gui, 3);
        Assert.Empty(logDay.CacDong);
        await gui.GuiTatCaAsync();
        Assert.Equal(3, thu.Telegram.Tin.Count);
        Assert.Equal(0, thu.Kho.SoLanDoc);
        Assert.Equal(1, logDay.Dem(LogLevel.Warning));
        Assert.Equal(0, thu.Log.Dem(LogLevel.Warning));
    }

    /// <summary>Proves both sends start before either channel is allowed to complete.</summary>
    /// <returns>Completion of concurrent delivery assertions.</returns>
    [Fact]
    public async Task HaiKenhBatDauSongSong()
    {
        var thu = new BoThuGuiThongBao();
        var telegramDaVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dayDaVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Telegram.KhiGui = async huy =>
        {
            telegramDaVao.TrySetResult();
            await dayDaVao.Task.WaitAsync(huy);
        };
        thu.Day.KhiGui = async huy =>
        {
            dayDaVao.TrySetResult();
            await telegramDaVao.Task.WaitAsync(huy);
            return new(1, []);
        };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Single(thu.Day.Tin);
        Assert.Single(thu.Telegram.Tin);
        Assert.Equal(0, thu.Log.Dem(LogLevel.Warning));
    }
}
