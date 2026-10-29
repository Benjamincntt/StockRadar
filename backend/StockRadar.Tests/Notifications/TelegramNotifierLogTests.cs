using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure;

namespace StockRadar.Tests.Notifications;

public sealed class TelegramNotifierLogTests
{
    [Fact]
    public async Task DangKyThat_GuiTin_KhongGhiUrlHoacTokenVaoLog()
    {
        const string tokenGia = "TOK-SECRET-123";
        using var thuLog = new BoThuLog();
        using var xuLyGia = new BoXuLyHttpGia();
        var dichVu = new ServiceCollection();
        dichVu.AddLogging(cauHinh => cauHinh
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(thuLog));
        dichVu.Configure<TelegramNotifyOptions>(cauHinh =>
        {
            cauHinh.Enabled = true;
            cauHinh.BotToken = tokenGia;
            cauHinh.ChatId = "chat-test";
        });
        dichVu.AddTelegramNotifier();
        dichVu.Configure<HttpClientFactoryOptions>(nameof(ITelegramNotifier), cauHinh =>
            cauHinh.HttpMessageHandlerBuilderActions.Add(boDung =>
                boDung.PrimaryHandler = xuLyGia));

        using var nhaCungCap = dichVu.BuildServiceProvider();
        nhaCungCap.GetRequiredService<ILoggerFactory>()
            .CreateLogger("KiemTraThuLog")
            .LogInformation("Log capture is active.");
        var kenhTelegram = nhaCungCap.GetRequiredService<ITelegramNotifier>();

        await kenhTelegram.SendAsync("Notification regression test");

        Assert.Equal(1, xuLyGia.SoLanGoi);
        Assert.True(thuLog.CacDong.Any(dong => dong.DanhMuc == "KiemTraThuLog"),
            "The test logger must capture enabled log output.");
        Assert.True(thuLog.CacDong.Any(dong =>
                dong.DanhMuc == kenhTelegram.GetType().FullName && dong.Muc == LogLevel.Information),
            "Successful Telegram delivery must still produce the notifier's own log.");

        var soDongLoUrl = thuLog.CacDong.Count(dong =>
            dong.NoiDung.Contains("api.telegram.org/bot", StringComparison.OrdinalIgnoreCase));
        var soDongLoToken = thuLog.CacDong.Count(dong =>
            dong.NoiDung.Contains(tokenGia, StringComparison.Ordinal));
        Assert.True(soDongLoUrl == 0 && soDongLoToken == 0,
            $"Sensitive HTTP log entries detected: URL={soDongLoUrl}, token={soDongLoToken}. Values withheld.");
    }

    private sealed class BoXuLyHttpGia : HttpMessageHandler
    {
        public int SoLanGoi { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage yeuCau, CancellationToken huy)
        {
            SoLanGoi++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        }
    }

    private sealed record DongLog(string DanhMuc, LogLevel Muc, string NoiDung);

    private sealed class BoThuLog : ILoggerProvider
    {
        public ConcurrentQueue<DongLog> CacDong { get; } = new();

        public ILogger CreateLogger(string danhMuc) => new BoGhiLog(danhMuc, CacDong);

        public void Dispose() { }

        private sealed class BoGhiLog(string danhMuc, ConcurrentQueue<DongLog> cacDong) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState trangThai) where TState : notnull => null;

            public bool IsEnabled(LogLevel muc) => muc != LogLevel.None;

            public void Log<TState>(LogLevel muc, EventId maSuKien, TState trangThai,
                Exception? loi, Func<TState, Exception?, string> dinhDang)
            {
                if (IsEnabled(muc))
                    cacDong.Enqueue(new DongLog(danhMuc, muc, dinhDang(trangThai, loi)));
            }
        }
    }
}
