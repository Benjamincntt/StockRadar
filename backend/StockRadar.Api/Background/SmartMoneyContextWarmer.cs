using StockRadar.Application.Services;

namespace StockRadar.Api.Background;

public sealed class SmartMoneyContextWarmer(
    IServiceScopeFactory scopeFactory,
    ILogger<SmartMoneyContextWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kỳ warm 3 phút < TTL 5 phút (Cache:SmartMoneyContextSeconds) → cache luôn được
        // thay bằng bản mới trước khi hết hạn; sync trong phiên không còn xóa context nữa.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(3));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var eval = scope.ServiceProvider.GetRequiredService<SmartMoneyEvaluationService>();
                await eval.RefreshContextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Warm SmartMoney context thất bại");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
