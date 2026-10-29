using StockRadar.Domain.Enums;

namespace StockRadar.Tests.Notifications;

public sealed class GuiThongBaoTamTests
{
    /// <summary>Buffering neither sends nor queries devices, and each flush consumes its messages once.</summary>
    /// <returns>Completion of order and duplicate assertions.</returns>
    [Fact]
    public async Task ChiGuiKhiXaTheoThuTuVaKhongGuiLap()
    {
        var thu = new BoThuGuiThongBao();
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 3);
        Assert.Empty(thu.Telegram.Tin);
        Assert.Empty(thu.Day.Tin);
        Assert.Equal(0, thu.Kho.SoLanDoc);
        await gui.GuiTatCaAsync();
        Assert.Equal(new[] { "<b>tin-0</b>", "<b>tin-1</b>", "<b>tin-2</b>" }, thu.Telegram.Tin.Select(tin => tin.NoiDung));
        Assert.Equal(new[] { "tin-0", "tin-1", "tin-2" }, thu.Day.Tin.Select(tin => tin.NoiDung.NoiDung));
        Assert.All(thu.Telegram.Tin, tin => Assert.Equal("HTML", tin.DinhDang));
        Assert.All(thu.Day.Tin, tin => Assert.Equal(LoaiTinV3.Mua, tin.NoiDung.LoaiV3));
        Assert.Equal(1, thu.Kho.SoLanDoc);
        await gui.GuiTatCaAsync();
        Assert.Equal(3, thu.Telegram.Tin.Count);
        Assert.Equal(3, thu.Day.Tin.Count);
        Assert.Equal(1, thu.Kho.SoLanDoc);
    }

    /// <summary>System buffering preserves plain text, and immediate HPO flushes in buffered order.</summary>
    /// <returns>Completion of system-message assertions.</returns>
    [Fact]
    public async Task HeThongGiuNguyenChuVaGuiNgayTheoThuTu()
    {
        var thu = new BoThuGuiThongBao();
        var gui = thu.Tao();
        var noiDung = "HPO <literal> & text\n\n" + new string('x', 300);
        gui.DatTinHeThong("HPO · xong", noiDung);
        Assert.Empty(thu.Telegram.Tin);
        await gui.GuiHeThongAsync("HPO · bỏ qua", "skip");
        Assert.Equal(new[] { noiDung, "skip" }, thu.Telegram.Tin.Select(tin => tin.NoiDung));
        Assert.All(thu.Telegram.Tin, tin => Assert.Null(tin.DinhDang));
        Assert.Equal(noiDung, thu.Day.Tin[0].NoiDung.NoiDung);
        Assert.All(thu.Day.Tin, tin =>
        {
            Assert.Equal("he-thong", tin.NoiDung.Kenh);
            Assert.Null(tin.NoiDung.Symbol);
            Assert.Null(tin.NoiDung.LoaiV3);
        });
        await gui.GuiTatCaAsync();
        Assert.Equal(2, thu.Telegram.Tin.Count);
    }

    /// <summary>An empty device list skips push while still sending each Telegram message.</summary>
    /// <returns>Completion of empty-recipient assertions.</returns>
    [Fact]
    public async Task KhongCoMayVanGuiTelegram()
    {
        var thu = new BoThuGuiThongBao();
        thu.Kho.CacToken.Clear();
        var gui = thu.Tao();
        BoThuGuiThongBao.Gom(gui, 2);
        await gui.GuiTatCaAsync();
        Assert.Equal(2, thu.Telegram.Tin.Count);
        Assert.Empty(thu.Day.Tin);
        Assert.Equal(1, thu.Kho.SoLanDoc);
    }
}
