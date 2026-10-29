using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class KiemTraTinFcmTests
{
    public static TheoryData<string?, string?, string?, string?, LoaiTinV3?> TinSai => new()
    {
        { null, "body", "v3-signal", null, null },
        { "", "body", "v3-signal", null, null },
        { " \t", "body", "v3-signal", null, null },
        { "title", null, "v3-signal", null, null },
        { "title", "", "v3-signal", null, null },
        { "title", "\r\n ", "v3-signal", null, null },
        { "title", "body", null, null, null },
        { "title", "body", "unknown-private-channel", null, null },
        { "title", "body", "V3-SIGNAL", null, null },
        { "title", "body", "he-thong", "HPG", null },
        { "title", "body", "he-thong", null, LoaiTinV3.Mua },
        { "title", "body", "v3-signal", "HPG", null },
        { "title", "body", "v3-signal", "", null },
        { "title", "body", "v3-signal", null, LoaiTinV3.Mua },
        { "title", "body", "v3-signal", "HPG", (LoaiTinV3)999 },
        { "title", "body", "v3-signal", "hpg", LoaiTinV3.Mua },
        { "title", "body", "v3-signal", " HPG", LoaiTinV3.Mua },
        { "title", "body", "v3-signal", "HPG\n", LoaiTinV3.Mua },
        { "title", "body", "v3-signal", "H/P", LoaiTinV3.Mua },
        { "title", "body", "v3-signal", "HÉG", LoaiTinV3.Mua },
        { "title", "body", "v3-signal", new string('A', 17), LoaiTinV3.Mua },
        { "title", "body", "he-thong", "", null },
        { "title", "body", "v3-signal", "123", LoaiTinV3.Ban }
    };

    [Theory]
    [MemberData(nameof(TinSai))]
    public async Task TinSaiBoQuaKhongKhoiTaoKhongXoaTokenVaTinSauVanGui(
        string? tieuDe, string? noiDung, string? kenhTin, string? ma, LoaiTinV3? loai)
    {
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        var cacTin = new List<Message>();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            soKhoiTao++;
            return (tin, _) => { cacTin.Add(tin); return Task.FromResult("ok"); };
        });
        var tinSai = new DayTinNhan(tieuDe!, noiDung!, kenhTin!, ma, loai);
        Assert.False(KenhDayTinFcm.KiemTraTin(tinSai));
        Assert.False(KenhDayTinFcm.KiemTraTin(tinSai));
        Assert.Empty(thuLog.CacDong);
        var ketQua = await kenh.GuiAsync(["synthetic-device-private"], tinSai);
        Assert.Equal(0, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(cacTin);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Contains(thuLog.CacDong, dong => dong.ThuocTinh.Any(cap =>
            cap.Key == "LoaiLoi" && Equals(cap.Value, "payload")));
        var tinSau = await kenh.GuiAsync(["synthetic-device-private"], BoThuLogFcm.TinThu);
        Assert.Equal(1, tinSau.SoDaGui);
        Assert.Empty(tinSau.TokenHong);
        thuLog.KiemTraKhongLo("synthetic-device-private", "unknown-private-channel", "body");
    }

    [Fact]
    public Task Utf16HongKhongBiThayBangKyTuKhac() =>
        TinSaiBoQuaKhongKhoiTaoKhongXoaTokenVaTinSauVanGui("title", new string((char)0xD800, 1), "v3-signal", null, null);

    [Fact]
    public async Task TinNullBoQuaKhongNemLoi()
    {
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            soKhoiTao++;
            return (_, _) => Task.FromResult("ok");
        });
        Assert.False(KenhDayTinFcm.KiemTraTin(null!));
        var ketQua = await kenh.GuiAsync(["synthetic-device"], null!);
        Assert.Equal(0, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(0, soKhoiTao);
    }

    public static TheoryData<string, int> BienByte => new()
    {
        { "x", -1 }, { "x", 0 }, { "x", 1 },
        { "ệ", -1 }, { "ệ", 0 }, { "ệ", 1 },
        { "🚀", -1 }, { "🚀", 0 }, { "🚀", 1 },
        { "\"", -1 }, { "\"", 0 }, { "\"", 1 },
        { "\\", -1 }, { "\\", 0 }, { "\\", 1 }
    };

    [Theory]
    [MemberData(nameof(BienByte))]
    public async Task GioiHan4096TinhCaKhoaGiaTriVaUtf8(string phanTu, int lech)
    {
        var noiDung = TaoNoiDungTaiBien(phanTu, 4096 + lech);
        var cacTin = new List<Message>();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(),
            () => (tin, _) => { cacTin.Add(tin); return Task.FromResult("ok"); });
        var ketQua = await kenh.GuiAsync(["synthetic-device"], new DayTinNhan("T", noiDung, "v3-signal", null));
        Assert.Equal(lech <= 0 ? 1 : 0, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(lech <= 0 ? 1 : 0, cacTin.Count);
        if (lech <= 0)
            Assert.Equal(noiDung, cacTin.Single().Android.Notification.Body);
    }

    [Fact]
    public async Task TieuDeVaDuLieuDieuHuongCungTinhVaoGioiHan()
    {
        var noiDung = TaoNoiDungTaiBien("x", 4096);
        var soGui = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(),
            () => (_, _) => { soGui++; return Task.FromResult("ok"); });
        var daiTieuDe = await kenh.GuiAsync(["synthetic-device"], new DayTinNhan("TT", noiDung, "v3-signal", null));
        var coDieuHuong = await kenh.GuiAsync(["synthetic-device"], new DayTinNhan("T", noiDung, "v3-signal", "HPG", LoaiTinV3.Ban));
        Assert.Equal(0, daiTieuDe.SoDaGui);
        Assert.Equal(0, coDieuHuong.SoDaGui);
        Assert.Empty(daiTieuDe.TokenHong);
        Assert.Empty(coDieuHuong.TokenHong);
        Assert.Equal(0, soGui);
    }

    private static string TaoNoiDungTaiBien(string phanTu, int soByte)
    {
        const string khung = "{\"message\":{\"android\":{\"priority\":\"high\",\"ttl\":\"3600s\",\"notification\":{\"channel_id\":\"v3-signal\",\"title\":\"T\",\"body\":BODY}},\"data\":{\"nguon\":\"thu\"}}}";
        var tuyChon = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var noiDung = string.Concat(Enumerable.Repeat(phanTu, 100));
        var hienTai = Encoding.UTF8.GetByteCount(khung.Replace("BODY", JsonSerializer.Serialize(noiDung, tuyChon)));
        noiDung += new string('x', soByte - hienTai);
        Assert.Equal(soByte, Encoding.UTF8.GetByteCount(khung.Replace("BODY", JsonSerializer.Serialize(noiDung, tuyChon))));
        return noiDung;
    }
}
