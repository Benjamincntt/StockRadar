using FirebaseAdmin.Messaging;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class FcmMessageBuilderTests
{
    /// <summary>Verifies each V3 kind maps to the Android and navigation contract without guessing from its title.</summary>
    /// <param name="loai">The explicit notification kind.</param>
    /// <param name="giaTri">The expected client routing value.</param>
    [Theory]
    [InlineData(LoaiTinV3.Mua, "mua")]
    [InlineData(LoaiTinV3.Ban, "ban")]
    [InlineData(LoaiTinV3.HuyMua, "huy_mua")]
    [InlineData(LoaiTinV3.AtoN3, "ato_n3")]
    public void TinV3DungMucKenhThoiHanVaDuLieu(LoaiTinV3 loai, string giaTri)
    {
        var tin = new DayTinNhan("Tiêu đề độc lập", "Giá 27.3 — lý do", "v3-signal", "HPG", loai);
        var tinSdk = KenhDayTinFcm.TaoTinNhan("thiet-bi-gia-12345", tin);

        KiemTraNoiDung(tinSdk, tin);
        Assert.Equal(Priority.High, tinSdk.Android.Priority);
        Assert.Equal("v3-signal", tinSdk.Android.Notification.ChannelId);
        Assert.Equal(TimeSpan.FromHours(1), tinSdk.Android.TimeToLive);
        Assert.Equal(3, tinSdk.Data.Count);
        Assert.Equal("HPG", tinSdk.Data["symbol"]);
        Assert.Equal(giaTri, tinSdk.Data["loaiTin"]);
        Assert.Equal("v3", tinSdk.Data["nguon"]);
    }

    /// <summary>Preserves the HPO body exactly and emits only system-source data.</summary>
    [Fact]
    public void TinHeThongGiuNguyenNoiDungVaSongMuoiHaiGio()
    {
        var noiDung = "HPO · xong\r\n\r\nGiữ <b>nguyên</b> &amp; dữ liệu " + new string('x', 300);
        var tin = new DayTinNhan("HPO · xong", noiDung, "he-thong", null);
        var tinSdk = KenhDayTinFcm.TaoTinNhan("thiet-bi-gia-12345", tin);

        KiemTraNoiDung(tinSdk, tin);
        Assert.Equal(Priority.Normal, tinSdk.Android.Priority);
        Assert.Equal("he-thong", tinSdk.Android.Notification.ChannelId);
        Assert.Equal(TimeSpan.FromHours(12), tinSdk.Android.TimeToLive);
        Assert.Single(tinSdk.Data);
        Assert.Equal("he_thong", tinSdk.Data["nguon"]);
    }

    /// <summary>Routes app-test messages to the high-priority V3 channel with no stock navigation.</summary>
    [Fact]
    public void TinThuChiCoNguonThuVaSongMotGio()
    {
        var tin = BoThuLogFcm.TinThu;
        var tinSdk = KenhDayTinFcm.TaoTinNhan("thiet-bi-gia-12345", tin);

        KiemTraNoiDung(tinSdk, tin);
        Assert.Equal(Priority.High, tinSdk.Android.Priority);
        Assert.Equal("v3-signal", tinSdk.Android.Notification.ChannelId);
        Assert.Equal(TimeSpan.FromHours(1), tinSdk.Android.TimeToLive);
        Assert.Single(tinSdk.Data);
        Assert.Equal("thu", tinSdk.Data["nguon"]);
    }

    [Theory]
    [InlineData("v3-signal", "HPG", LoaiTinV3.Mua)]
    [InlineData("v3-signal", "FPT", LoaiTinV3.Ban)]
    [InlineData("v3-signal", "HPG", LoaiTinV3.HuyMua)]
    [InlineData("v3-signal", "HPG", LoaiTinV3.AtoN3)]
    [InlineData("he-thong", null, null)]
    [InlineData("v3-signal", null, null)]
    public async Task TinHopLeGuiDayDuKhongGhiNoiDungVaoLog(string kenhTin, string? ma, LoaiTinV3? loai)
    {
        var thuLog = new BoThuLogFcm();
        var cacTin = new List<Message>();
        var noiDung = "private-body C:/private/key.json https://example.invalid/secret?token=private\n" + new string('ệ', 500);
        var tin = new DayTinNhan("private-title", noiDung, kenhTin, ma, loai);
        Assert.True(KenhDayTinFcm.KiemTraTin(tin));
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (tinGui, _) => { cacTin.Add(tinGui); return Task.FromResult("ok"); });
        var ketQua = await kenh.GuiAsync(["synthetic-device"], tin);
        Assert.Equal(1, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(noiDung, Assert.Single(cacTin).Android.Notification.Body);
        thuLog.KiemTraKhongLo("private-title", "private-body", "C:/private/key.json", "example.invalid", "synthetic-device");
    }

    /// <summary>Checks per-device targeting and Android-local title and body placement.</summary>
    /// <param name="tinSdk">The built SDK message.</param>
    /// <param name="tin">The original notification.</param>
    private static void KiemTraNoiDung(Message tinSdk, DayTinNhan tin)
    {
        Assert.True(KenhDayTinFcm.KiemTraTin(tin));
        Assert.Equal("thiet-bi-gia-12345", tinSdk.Token);
        Assert.Null(tinSdk.Topic);
        Assert.Null(tinSdk.Condition);
        Assert.Null(tinSdk.Notification);
        Assert.NotNull(tinSdk.Android);
        Assert.NotNull(tinSdk.Android.Notification);
        Assert.Equal(tin.TieuDe, tinSdk.Android.Notification.Title);
        Assert.Equal(tin.NoiDung, tinSdk.Android.Notification.Body);
    }
}
