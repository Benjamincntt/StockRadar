using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Đồng bộ universe theo giá + thanh khoản — chạy cuối Job 1 hoặc thủ công.</summary>
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
            cfg.MinClosePriceVnd);

        var all = await stocks.GetAllForUniverseScreeningAsync(cancellationToken);
        var activeBefore = all.Count(s => s.IsActive);
        var deactivated = 0;
        var reactivated = 0;
        var updatedAt = DateTime.UtcNow;

        foreach (var stock in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screen = StockUniverseFilter.ScreenQuality(stock.History, settings);

            if (screen.Passes)
            {
                if (!stock.IsActive)
                {
                    await writer.MarkUniverseActiveAsync(
                        stock.Symbol, screen.AvgVolume30d, updatedAt, cancellationToken);
                    reactivated++;
                    logger.LogInformation("Universe khôi phục {Symbol}", stock.Symbol);
                }

                continue;
            }

            if (!stock.IsActive)
                continue;

            await writer.MarkUniverseInactiveAsync(stock.Symbol, screen.Reason, updatedAt, cancellationToken);
            deactivated++;
            logger.LogInformation("Universe loại {Symbol}: {Reason}", stock.Symbol, screen.Reason);
        }

        if (deactivated > 0 || reactivated > 0)
        {
            logger.LogInformation(
                "Universe rescreen: active {Before} → loại {Deactivated}, khôi phục {Reactivated} (giá >{MinPrice:N0}đ, TB KL≥{MinVol:N0}/{Sessions} phiên).",
                activeBefore,
                deactivated,
                reactivated,
                cfg.MinClosePriceVnd,
                cfg.MinAvgDailyVolume,
                cfg.VolumeLookbackSessions);
        }

        return new UniverseRescreenResultDto(activeBefore, deactivated, reactivated, updatedAt);
    }
}
