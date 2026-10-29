using Google.Apis.Http;

namespace StockRadar.Infrastructure.Notifications;

internal sealed class BoTaoHttpFcm : HttpClientFactory
{
    private readonly Func<HttpMessageHandler>? taoBoXuLy;

    internal BoTaoHttpFcm(Func<HttpMessageHandler>? taoBoXuLy = null) => this.taoBoXuLy = taoBoXuLy;

    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs thamSo)
    {
        thamSo.Initializers.Add(new GioiHanMotLan());
        return taoBoXuLy?.Invoke() ?? base.CreateHandler(thamSo);
    }

    private sealed class GioiHanMotLan : IConfigurableHttpClientInitializer
    {
        public void Initialize(ConfigurableHttpClient mayKhach)
        {
            // DO-NOT-CHANGE: Restoring the SDK's default four retries can resend a notification that FCM already accepted.
            mayKhach.MessageHandler.NumTries = 1;
        }
    }
}
