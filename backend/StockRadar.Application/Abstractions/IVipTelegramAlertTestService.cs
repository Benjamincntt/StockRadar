namespace StockRadar.Application.Abstractions;

public interface IVipTelegramAlertTestService
{
    /// <summary>Gửi mẫu tin VIP (fake data) — chỉ Telegram, không ghi DB.</summary>
    Task<VipTelegramTestResultDto> SendSampleAlertsAsync(CancellationToken cancellationToken = default);
}

public sealed record VipTelegramTestResultDto(
    int MessagesSent,
    IReadOnlyList<string> Scenarios,
    string? Error = null);
