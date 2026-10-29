using System.Net;
using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Microsoft.Extensions.Logging;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class KenhDayTinFcmTokenTests
{
    /// <summary>Returns only rejected registrations and continues with healthy devices and later messages.</summary>
    /// <param name="maLoi">The real SDK token error to synthesize over offline HTTP.</param>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Theory]
    [InlineData(MessagingErrorCode.Unregistered)]
    [InlineData(MessagingErrorCode.InvalidArgument)]
    public async Task TokenHongKhongChanMayKhoeVaTinSau(MessagingErrorCode maLoi)
    {
        var loi = await TaoLoiSdkAsync(maLoi);
        var thuLog = new BoThuLogFcm();
        string[] cacToken = ["thiet-bi-gia-khoe-11111", "thiet-bi-gia-hong-54321", "thiet-bi-gia-khoe-33333"];
        var daGoi = new List<string>();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => (tin, _) =>
        {
            daGoi.Add(tin.Token);
            return tin.Token == cacToken[1] ? Task.FromException<string>(loi) : Task.FromResult("ma-tin-gia");
        });

        var ketQua = await kenh.GuiAsync(cacToken, BoThuLogFcm.TinThu);
        Assert.Equal(2, ketQua.SoDaGui);
        Assert.Equal(maLoi == MessagingErrorCode.Unregistered ? new[] { cacToken[1] } : [], ketQua.TokenHong);
        Assert.Equal(cacToken, daGoi);
        Assert.True(kenh.DangHoatDong);
        var tokenTinSau = cacToken.Except(ketQua.TokenHong).ToArray();
        var tinSau = await kenh.GuiAsync(tokenTinSau, BoThuLogFcm.TinThu);
        Assert.Equal(2, tinSau.SoDaGui);
        Assert.Empty(tinSau.TokenHong);
        Assert.Equal(cacToken.Concat(tokenTinSau), daGoi);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(maLoi == MessagingErrorCode.Unregistered ? 0 : 1, thuLog.Dem(LogLevel.Debug));
        Assert.Contains(thuLog.CacDong, dong => dong.Muc == LogLevel.Warning && dong.NoiDung.Contains("54321") &&
            dong.ThuocTinh.Any(thuocTinh => thuocTinh.Key == "MessagingErrorCode" && thuocTinh.Value?.ToString() == maLoi.ToString()));
        Assert.Contains(thuLog.CacDong, dong => dong.Muc == LogLevel.Information &&
            dong.ThuocTinh.Any(thuocTinh => thuocTinh.Key == "SoDaGui" && Equals(thuocTinh.Value, 2)));
        thuLog.KiemTraKhongLo(cacToken.Concat(new[] { "loi-sdk-gia-khong-duoc-ghi" }).ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CanhBaoMoiTokenMotLanQuaBaTin(int soToken)
    {
        var loi = await TaoLoiSdkAsync(MessagingErrorCode.InvalidArgument);
        var thuLog = new BoThuLogFcm();
        var cacToken = new[] { "thiet-bi-gia-a-54321", "thiet-bi-gia-b-54321" }.Take(soToken).ToArray();
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => (_, _) =>
        {
            soGoi++;
            return Task.FromException<string>(loi);
        });
        for (var lan = 0; lan < 3; lan++)
        {
            var ketQua = await kenh.GuiAsync(cacToken, BoThuLogFcm.TinThu);
            Assert.Equal(0, ketQua.SoDaGui);
            Assert.Empty(ketQua.TokenHong);
        }
        Assert.Equal(3 * soToken, soGoi);
        Assert.Equal(soToken, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(2 * soToken, thuLog.Dem(LogLevel.Debug));
        thuLog.KiemTraKhongLo(cacToken);
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, "token")]
    [InlineData(MessagingErrorCode.InvalidArgument, "token_hoac_payload")]
    public async Task CanhBaoPhanLoaiDungLoiToken(MessagingErrorCode maLoi, string loaiLoi)
    {
        var loi = await TaoLoiSdkAsync(maLoi);
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromException<string>(loi));
        await kenh.GuiAsync(["thiet-bi-gia-54321"], BoThuLogFcm.TinThu);
        var dong = Assert.Single(thuLog.CacDong, dong => dong.Muc == LogLevel.Warning);
        Assert.Equal(loaiLoi, dong.ThuocTinh.Single(cap => cap.Key == "LoaiLoi").Value);
        thuLog.KiemTraKhongLo("thiet-bi-gia-54321", "loi-sdk-gia-khong-duoc-ghi");
    }

    [Fact]
    public async Task CanhBaoDongThoiCungTokenChiMotLan()
    {
        var loi = await TaoLoiSdkAsync(MessagingErrorCode.InvalidArgument);
        var thuLog = new BoThuLogFcm();
        var daVao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choPhep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => async (_, _) =>
        {
            if (Interlocked.Increment(ref soGoi) == 16) daVao.TrySetResult();
            await choPhep.Task;
            throw loi;
        });
        var cacViec = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            kenh.GuiAsync(["thiet-bi-gia-54321"], BoThuLogFcm.TinThu))).ToArray();
        try { await daVao.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { choPhep.TrySetResult(); await Task.WhenAll(cacViec); }
        Assert.Equal(16, soGoi);
        Assert.All(cacViec, viec => Assert.Empty(viec.Result.TokenHong));
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(15, thuLog.Dem(LogLevel.Debug));
        thuLog.KiemTraKhongLo("thiet-bi-gia-54321", "loi-sdk-gia-khong-duoc-ghi");
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered)]
    [InlineData(MessagingErrorCode.InvalidArgument)]
    public async Task TokenNganDuocCheVaMaLoiDuocGhi(MessagingErrorCode maLoi)
    {
        var loi = await TaoLoiSdkAsync(maLoi);
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromException<string>(loi));
        var ketQua = await kenh.GuiAsync(["tiny"], BoThuLogFcm.TinThu);
        Assert.Equal(maLoi == MessagingErrorCode.Unregistered ? new[] { "tiny" } : [], ketQua.TokenHong);
        Assert.Contains(thuLog.CacDong, dong => dong.NoiDung.Contains("[redacted]") &&
            dong.NoiDung.Contains(maLoi.ToString()));
        thuLog.KiemTraKhongLo("tiny", "loi-sdk-gia-khong-duoc-ghi");
    }

    /// <summary>Propagates nullable or nontoken SDK errors unchanged without a channel warning or retry.</summary>
    /// <param name="maLoi">The nontoken SDK code, or no messaging-specific code.</param>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData(MessagingErrorCode.ThirdPartyAuthError)]
    public async Task LoiSdkKhacVaMaNullPhaiTruyenLen(MessagingErrorCode? maLoi)
    {
        var loi = await TaoLoiSdkAsync(maLoi);
        var thuLog = new BoThuLogFcm();
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => (_, _) =>
        {
            soGoi++;
            return Task.FromException<string>(loi);
        });

        var loiNhan = await Assert.ThrowsAsync<FirebaseMessagingException>(() =>
            kenh.GuiAsync(["thiet-bi-gia-dau", "thiet-bi-gia-sau"], BoThuLogFcm.TinThu));
        Assert.Same(loi, loiNhan);
        Assert.Equal(1, soGoi);
        Assert.True(kenh.DangHoatDong);
        Assert.Empty(thuLog.CacDong);
    }

    /// <summary>Propagates connection failures once and leaves outage warnings and retries to the dispatcher.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task LoiKetNoiKhongCanhBaoKhongThuLai()
    {
        var loi = new HttpRequestException("ket-noi-gia-bi-mat");
        var thuLog = new BoThuLogFcm();
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => (_, _) =>
        {
            soGoi++;
            return soGoi == 1 ? Task.FromException<string>(loi) : Task.FromResult("ma-tin-gia");
        });

        var loiNhan = await Assert.ThrowsAsync<HttpRequestException>(() =>
            kenh.GuiAsync(["thiet-bi-gia-dau", "thiet-bi-gia-sau"], BoThuLogFcm.TinThu));
        Assert.Same(loi, loiNhan);
        Assert.Equal(1, soGoi);
        Assert.Empty(thuLog.CacDong);
        Assert.True(kenh.DangHoatDong);
        var lanSau = await kenh.GuiAsync(["thiet-bi-gia-sau"], BoThuLogFcm.TinThu);
        Assert.Equal(1, lanSau.SoDaGui);
        Assert.Equal(2, soGoi);
        Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
        thuLog.KiemTraKhongLo("ket-noi-gia-bi-mat");
    }

    /// <summary>Checks cancellation before initialization, including an empty dispatcher send.</summary>
    /// <param name="rong">Whether the dispatcher supplied no devices.</param>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HuyTruocGuiKhongKhoiTao(bool rong)
    {
        using var huy = new CancellationTokenSource();
        huy.Cancel();
        var soKhoiTao = 0;
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            soKhoiTao++;
            return (_, _) => Task.FromResult("ma-tin-gia");
        });
        var loi = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            kenh.GuiAsync(rong ? [] : ["thiet-bi-gia"], BoThuLogFcm.TinThu, huy.Token));
        Assert.Equal(huy.Token, loi.CancellationToken);
        Assert.Equal(0, soKhoiTao);
        Assert.Empty(thuLog.CacDong);
    }

    /// <summary>Passes the caller token into the sender and stops remaining devices during cancellation.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task HuyTrongGuiTruyenTokenVaDungCacMaySau()
    {
        using var huy = new CancellationTokenSource();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thuLog = new BoThuLogFcm();
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => async (_, huyGui) =>
        {
            soGoi++;
            Assert.Equal(huy.Token, huyGui);
            daBatDau.SetResult();
            await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(huyGui);
            return "khong-duoc-tra-ve";
        });
        var dangGui = kenh.GuiAsync(["thiet-bi-gia-dau", "thiet-bi-gia-sau"], BoThuLogFcm.TinThu, huy.Token);
        await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
        huy.Cancel();
        var loi = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dangGui);
        Assert.Equal(huy.Token, loi.CancellationToken);
        Assert.Equal(1, soGoi);
        Assert.Empty(thuLog.CacDong);
    }

    /// <summary>Observes cancellation between sends even when the previous sender ignores cancellation.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task HuyGiuaHaiMayKhongGoiMaySau()
    {
        using var huy = new CancellationTokenSource();
        var thuLog = new BoThuLogFcm();
        var soGoi = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => (_, _) =>
        {
            soGoi++;
            huy.Cancel();
            return Task.FromResult("ma-tin-gia");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            kenh.GuiAsync(["thiet-bi-gia-dau", "thiet-bi-gia-sau"], BoThuLogFcm.TinThu, huy.Token));
        Assert.Equal(1, soGoi);
        Assert.Empty(thuLog.CacDong);
    }

    /// <summary>Awaits each send before starting the next and counts successful sends exactly once.</summary>
    /// <returns>The completed asynchronous assertion sequence.</returns>
    [Fact]
    public async Task GuiTuanTuVaDemThanhCong()
    {
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choPhep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daGoi = new List<string>();
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () => async (tin, _) =>
        {
            daGoi.Add(tin.Token);
            if (daGoi.Count == 1)
            {
                daBatDau.SetResult();
                await choPhep.Task;
            }
            return "ma-tin-gia";
        });
        string[] cacToken = ["thiet-bi-gia-dau", "thiet-bi-gia-sau"];
        var dangGui = kenh.GuiAsync(cacToken, BoThuLogFcm.TinThu);
        await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.Single(daGoi);
            Assert.False(dangGui.IsCompleted);
        }
        finally
        {
            choPhep.SetResult();
        }
        var ketQua = await dangGui;
        Assert.Equal(cacToken, daGoi);
        Assert.Equal(2, ketQua.SoDaGui);
        Assert.Empty(ketQua.TokenHong);
        Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
        kenh.Dispose();
        Assert.False(kenh.DangHoatDong);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => kenh.GuiAsync(cacToken, BoThuLogFcm.TinThu));
        Assert.Equal(2, daGoi.Count);
    }

    /// <summary>Produces a real typed SDK exception using a fake HTTP factory with no network or key material.</summary>
    /// <param name="maLoi">The messaging error code to return, or no FCM detail.</param>
    /// <returns>The actual exception decoded by FirebaseAdmin 3.7.0.</returns>
    internal static async Task<FirebaseMessagingException> TaoLoiSdkAsync(MessagingErrorCode? maLoi)
    {
        var maHttp = maLoi == MessagingErrorCode.Unregistered ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest;
        var tenLoi = maLoi switch
        {
            MessagingErrorCode.Unregistered => "UNREGISTERED",
            MessagingErrorCode.InvalidArgument => "INVALID_ARGUMENT",
            MessagingErrorCode.ThirdPartyAuthError => "THIRD_PARTY_AUTH_ERROR",
            _ => null
        };
        var chiTiet = tenLoi is null ? Array.Empty<Dictionary<string, string>>() :
            new[] { new Dictionary<string, string>
            {
                ["@type"] = "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                ["errorCode"] = tenLoi
            } };
        var phanHoi = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = (int)maHttp,
                message = "loi-sdk-gia-khong-duoc-ghi",
                status = maHttp == HttpStatusCode.NotFound ? "NOT_FOUND" : "INVALID_ARGUMENT",
                details = chiTiet
            }
        });
        var boTao = new BoTaoHttpGia(maHttp, phanHoi);
        var ungDung = FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromAccessToken("xac-thuc-gia-chi-dung-offline"),
            ProjectId = "du-an-gia-offline",
            HttpClientFactory = boTao
        }, "kiem-thu-" + Guid.NewGuid().ToString("N"));
        try
        {
            var guiSdk = FirebaseMessaging.GetMessaging(ungDung);
            var loi = await Assert.ThrowsAsync<FirebaseMessagingException>(() => guiSdk.SendAsync(new Message
            {
                Token = "thiet-bi-gia-offline",
                Notification = new Notification { Title = "Offline" }
            }));
            Assert.Equal(maLoi, loi.MessagingErrorCode);
            Assert.Equal(1, boTao.SoYeuCau);
            return loi;
        }
        finally
        {
            ungDung.Delete();
        }
    }

    private sealed class BoTaoHttpGia : HttpClientFactory
    {
        private readonly HttpStatusCode maHttp;
        private readonly string phanHoi;
        internal int SoYeuCau;

        /// <summary>Configures the fixed offline FCM response.</summary>
        /// <param name="maHttp">The simulated response status.</param>
        /// <param name="phanHoi">The simulated FCM error body.</param>
        internal BoTaoHttpGia(HttpStatusCode maHttp, string phanHoi)
        {
            this.maHttp = maHttp;
            this.phanHoi = phanHoi;
        }

        /// <summary>Replaces the SDK transport entirely, preventing external sends.</summary>
        /// <param name="thamSo">SDK HTTP construction arguments.</param>
        /// <returns>A handler with no underlying network transport.</returns>
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs thamSo) => new BoXuLyHttpGia(this);

        private sealed class BoXuLyHttpGia : HttpMessageHandler
        {
            private readonly BoTaoHttpGia boTao;

            /// <summary>Associates the offline handler with its response and request counter.</summary>
            /// <param name="boTao">The owning fake factory.</param>
            internal BoXuLyHttpGia(BoTaoHttpGia boTao) => this.boTao = boTao;

            /// <summary>Returns the fixed response without delegating to a socket or other handler.</summary>
            /// <param name="yeuCau">The SDK request.</param>
            /// <param name="huy">SDK cancellation.</param>
            /// <returns>The fixed FCM error response.</returns>
            /// <exception cref="OperationCanceledException">The request was canceled.</exception>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage yeuCau, CancellationToken huy)
            {
                huy.ThrowIfCancellationRequested();
                Interlocked.Increment(ref boTao.SoYeuCau);
                return Task.FromResult(new HttpResponseMessage(boTao.maHttp)
                {
                    Content = new StringContent(boTao.phanHoi, Encoding.UTF8, "application/json"),
                    RequestMessage = yeuCau
                });
            }
        }
    }
}
