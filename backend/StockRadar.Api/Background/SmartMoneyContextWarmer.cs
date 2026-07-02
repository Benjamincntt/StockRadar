using StockRadar.Application.Services;

namespace StockRadar.Api.Background;

public sealed class SmartMoneyContextWarmer(
    IServiceScopeFactory scopeFactory,
    ILogger<SmartMoneyContextWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(4));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var eval = scope.ServiceProvider.GetRequiredService<SmartMoneyEvaluationService>();
                await eval.BuildContextAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Warm SmartMoney context thất bại");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
