using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Loại mã rác khỏi universe theo giá + thanh khoản (Job 2 / thủ công).</summary>
internal sealed class UniverseRescreenRunner(
    IJobStockRepository stocks,
    IMarketDataWriter writer,
    IOptions<MarketJobsOptions> options,
    ILogger<UniverseRescreenRunner> logger) : IUniverseRescreenService
{
    public async Task<UniverseRescreenResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.History;
        var settings = new UniverseFilterSettings(
            cfg.MinAvgDailyVolume,
            cfg.VolumeLookbackSessions,
            cfg.ExcludeIpoWithinDays,
            cfg.MinClosePrice);

        var all = await stocks.GetAllAsync(cancellationToken);
        var activeBefore = all.Count;
        var deactivated = 0;
        var updatedAt = DateTime.UtcNow;

        foreach (var stock in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screen = StockUniverseFilter.ScreenQuality(stock.History, settings);
            if (screen.Passes)
                continue;

            await writer.MarkUniverseInactiveAsync(stock.Symbol, screen.Reason, updatedAt, cancellationToken);
            deactivated++;
            logger.LogInformation("Universe loại {Symbol}: {Reason}", stock.Symbol, screen.Reason);
        }

        if (deactivated > 0)
        {
            logger.LogInformation(
                "Universe rescreen: loại {Deactivated}/{Before} mã (giá >{MinPrice:N0}, TB KL≥{MinVol:N0}/{Sessions} phiên).",
                deactivated,
                activeBefore,
                cfg.MinClosePrice,
                cfg.MinAvgDailyVolume,
                cfg.VolumeLookbackSessions);
        }

        return new UniverseRescreenResultDto(activeBefore, deactivated, updatedAt);
    }
}
