using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

internal sealed class WebhookZaloNotifier(
    HttpClient http,
    IOptions<ZaloNotifyOptions> options,
    ILogger<WebhookZaloNotifier> logger) : IZaloNotifier
{
    public async Task SendAsync(string message, string? symbol = null, CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
        {
            logger.LogDebug("Zalo notify tắt — bỏ qua: {Symbol}", symbol);
            return;
        }

        if (string.IsNullOrWhiteSpace(cfg.WebhookUrl))
        {
            logger.LogWarning(
                "ZaloNotify:WebhookUrl trống — không gửi được tới {Phone}. Message: {Preview}",
                cfg.PhoneNumber,
                message.Length > 80 ? message[..80] + "…" : message);
            return;
        }

        var payload = new
        {
            phone = cfg.PhoneNumber,
            message,
            symbol,
            sentAt = DateTime.UtcNow
        };

        try
        {
            using var response = await http.PostAsJsonAsync(cfg.WebhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Zalo webhook HTTP {Status} {Symbol}", response.StatusCode, symbol);
            else
                logger.LogInformation("Đã gửi Zalo alert {Symbol} → {Phone}", symbol, cfg.PhoneNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Zalo webhook lỗi {Symbol}", symbol);
        }
    }
}
