using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

internal sealed class TelegramNotifier(
    HttpClient http,
    IOptions<TelegramNotifyOptions> options,
    ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
        {
            logger.LogDebug("Telegram notify tắt — bỏ qua.");
            return;
        }

        if (string.IsNullOrWhiteSpace(cfg.BotToken) || string.IsNullOrWhiteSpace(cfg.ChatId))
        {
            logger.LogWarning("TelegramNotify: BotToken hoặc ChatId trống — không gửi được.");
            return;
        }

        var url = $"https://api.telegram.org/bot{cfg.BotToken.Trim()}/sendMessage";
        var payload = new TelegramSendPayload(cfg.ChatId.Trim(), message);

        try
        {
            using var response = await http.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Telegram HTTP {Status}: {Body}", response.StatusCode, body);
            }
            else
            {
                logger.LogInformation("Đã gửi Telegram HPO alert.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Telegram gửi lỗi.");
        }
    }

    private sealed record TelegramSendPayload(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text);
}
