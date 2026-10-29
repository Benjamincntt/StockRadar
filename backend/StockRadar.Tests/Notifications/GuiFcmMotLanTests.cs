using System.Net;
using System.Text;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class GuiFcmMotLanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Loi503HoacKetNoiKhongBiSdkThuLai(bool loiKetNoi)
    {
        var boXuLy = new BoXuLyGia(loiKetNoi);
        var ungDung = FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromAccessToken("synthetic-offline-access-token"),
            ProjectId = "offline-fcm-no-retry",
            HttpClientFactory = new BoTaoHttpFcm(() => boXuLy)
        }, "offline-no-retry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var thuLog = new BoThuLogFcm();
            using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
                () => (tin, huy) => FirebaseMessaging.GetMessaging(ungDung).SendAsync(tin, huy));
            var loi = await Record.ExceptionAsync(() => kenh.GuiAsync(["synthetic-device"], BoThuLogFcm.TinThu));
            Assert.Equal(1, boXuLy.SoYeuCau);
            Assert.NotNull(loi);
            Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
            thuLog.KiemTraKhongLo("private-offline-failure", "synthetic-offline-access-token");
        }
        finally
        {
            ungDung.Delete();
        }
    }

    private sealed class BoXuLyGia(bool loiKetNoi) : HttpMessageHandler
    {
        internal int SoYeuCau;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage yeuCau, CancellationToken huy)
        {
            huy.ThrowIfCancellationRequested();
            var lan = Interlocked.Increment(ref SoYeuCau);
            if (lan == 1 && loiKetNoi)
                throw new HttpRequestException("private-offline-failure");
            return Task.FromResult(new HttpResponseMessage(lan == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                RequestMessage = yeuCau,
                Content = new StringContent(lan == 1
                    ? "{\"error\":{\"code\":503,\"message\":\"private-offline-failure\",\"status\":\"UNAVAILABLE\"}}"
                    : "{\"name\":\"projects/offline/messages/synthetic\"}", Encoding.UTF8, "application/json")
            });
        }
    }
}
