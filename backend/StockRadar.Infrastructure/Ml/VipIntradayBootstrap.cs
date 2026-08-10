using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Ml;

internal sealed class VipIntradayBootstrap(
    IVipIntradayRanker ranker,
    IVipIntradayCalibrationService calibration,
    IVipIntradayThresholdService thresholds,
    ILogger<VipIntradayBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ranker.ReloadModelAsync(cancellationToken);
        await calibration.ReloadAsync(cancellationToken);
        await thresholds.RefreshFromKpiAsync(cancellationToken);

        var snap = ranker.GetModelSnapshot();
        if (ranker.IsModelActive)
        {
            logger.LogInformation(
                "VipIntraday active: {Samples} mẫu, AUC {Acc:0.#}%.",
                snap.TrainingSamples,
                snap.TrainingAccuracy);
        }
        else
        {
            logger.LogInformation("VipIntraday: chưa train — gate dùng daily OpportunityRanker.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
