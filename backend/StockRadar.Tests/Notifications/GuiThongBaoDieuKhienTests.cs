using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class GuiThongBaoDieuKhienTests
{
    [Fact]
    public async Task TelegramTinHaiKhongChoDayTinMotVaMoiKenhGiuThuTu()
    {
        var thu = new BoThuGuiThongBao();
        var daVaoDay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choDay = new TaskCompletionSource<DayTinKetQua>(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Day.KhiGui = _ => { daVaoDay.TrySetResult(); return choDay.Task; };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 3);
        var viec = gui.GuiTatCaAsync();
        await daVaoDay.Task.WaitAsync(TimeSpan.FromSeconds(1));
        try
        {
            Assert.Equal(new[] { "<b>tin-0</b>", "<b>tin-1</b>", "<b>tin-2</b>" }, thu.Telegram.Tin.Select(tin => tin.NoiDung));
            Assert.Single(thu.Day.Tin);
        }
        finally { choDay.TrySetResult(new(1, [])); await viec; }
        Assert.Equal(new[] { "tin-0", "tin-1", "tin-2" }, thu.Day.Tin.Select(tin => tin.NoiDung.NoiDung));
    }

    [Fact]
    public async Task DayTinHaiKhongChoTelegramTinMot()
    {
        var thu = new BoThuGuiThongBao();
        var daGuiDay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choTelegram = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thu.Telegram.KhiGui = _ => choTelegram.Task;
        thu.Day.KhiGui = _ => { if (thu.Day.Tin.Count == 3) daGuiDay.TrySetResult(); return Task.FromResult(new DayTinKetQua(1, [])); };
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 3);
        var viec = gui.GuiTatCaAsync();
        try { await daGuiDay.Task.WaitAsync(TimeSpan.FromMilliseconds(500)); Assert.Single(thu.Telegram.Tin); }
        finally { choTelegram.TrySetResult(); await viec; }
    }

    [Theory]
    [InlineData("doc_token")]
    [InlineData("gui")]
    [InlineData("don_token")]
    public async Task LogChiChuaTruongChoPhepVaLoaiLoi(string giaiDoan)
    {
        var thu = new BoThuGuiThongBao();
        var loi = new HttpRequestException("synthetic-sensitive-exception C:/synthetic/credential.json");
        switch (giaiDoan)
        {
            case "doc_token": thu.Kho.KhiDoc = _ => throw loi; break;
            case "gui": thu.Day.KhiGui = _ => throw loi; break;
            case "don_token":
                thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(0, thu.Kho.CacToken.ToArray()));
                thu.Kho.KhiXoa = _ => throw loi; break;
        }
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 3);
        await gui.GuiTatCaAsync();
        var dong = Assert.Single(thu.Log.CacDong, muc => muc.Muc == LogLevel.Warning);
        Assert.Equal(giaiDoan, dong.Truong.GetValueOrDefault("GiaiDoan"));
        Assert.Equal("mang", dong.Truong.GetValueOrDefault("LoaiLoi"));
        Assert.Equal(nameof(HttpRequestException), dong.Truong.GetValueOrDefault("KieuLoi"));
        Assert.Contains("ThoiGianMs", dong.Truong.Keys);
        Assert.Contains("SoMayNhan", dong.Truong.Keys);
        Assert.All(thu.Log.CacDong, muc =>
        {
            Assert.Null(muc.Loi);
            Assert.DoesNotContain("synthetic-sensitive-exception", muc.NoiDung);
            Assert.DoesNotContain("C:/synthetic/credential.json", muc.NoiDung);
            Assert.All(muc.Truong.Keys, ten => Assert.Contains(ten, new[]
            {
                "{OriginalFormat}", "GiaiDoan", "LoaiLoi", "KieuLoi", "MessagingErrorCode", "ThoiGianMs", "SoMayNhan", "Symbol"
            }));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PayloadSaiChiBoTinDayDoKhongXoaDangKy(int truongHop)
    {
        var thu = new BoThuGuiThongBao();
        thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(0, thu.Kho.CacToken.ToArray()));
        var gui = thu.Tao();
        switch (truongHop)
        {
            case 0: gui.DatTinHeThong(" ", "body"); break;
            case 1: gui.DatTinV3(LoaiTinV3.Mua, "HPG", LoaiKichBanV3.N1, new string('x', 5000)); break;
            case 2: gui.DatTinV3(LoaiTinV3.Mua, "bad/symbol", LoaiKichBanV3.N1, "body"); break;
        }
        await gui.GuiTatCaAsync();
        Assert.Single(thu.Kho.CacToken);
        Assert.Single(thu.Telegram.Tin);
        Assert.Empty(thu.Day.Tin);
        Assert.Equal(0, thu.Kho.SoLanDoc);
        var canhBao = Assert.Single(thu.Log.CacDong, dong => dong.Muc == LogLevel.Warning);
        Assert.Equal("payload", canhBao.Truong.GetValueOrDefault("LoaiLoi"));
        Assert.Equal(truongHop == 0 ? null : truongHop == 1 ? "HPG" : "bad/symbol",
            canhBao.Truong.GetValueOrDefault("Symbol"));
        thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(1, []));
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Single(thu.Day.Tin);
    }

    [Fact]
    public async Task KhoiTaoFcmChamVanHetHanHaiGiayVaKhongGuiLaiTinCu()
    {
        var dongHo = new DongHoGia();
        var thu = new BoThuGuiThongBao { DongHo = dongHo };
        var daKhoiTao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choXong = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var khoiTaoXong = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soGui = 0;
        var soKhoiTao = 0;
        using var day = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            Interlocked.Increment(ref soKhoiTao);
            daKhoiTao.TrySetResult();
            choXong.Task.GetAwaiter().GetResult();
            khoiTaoXong.TrySetResult();
            return (_, _) => { Interlocked.Increment(ref soGui); return Task.FromResult("offline"); };
        }, dongHo);
        var gui = thu.Tao(day: day);
        BoThuGuiThongBao.Gom(gui, 3);
        var viec = gui.GuiTatCaAsync();
        await daKhoiTao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            dongHo.Tien(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(3, thu.Telegram.Tin.Count);
            Assert.Equal(0, soGui);
            var dong = Assert.Single(thu.Log.CacDong, muc => muc.Muc == LogLevel.Warning);
            Assert.Equal("khoi_tao", dong.Truong.GetValueOrDefault("GiaiDoan"));
            Assert.Equal("qua_han", dong.Truong.GetValueOrDefault("LoaiLoi"));
        }
        finally { choXong.TrySetResult(); await khoiTaoXong.Task; await viec; }
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(1, soGui);
    }
}
