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
            cfg.MinClosePriceVnd,
            cfg.MinAvgDailyValueVnd);

        var all = await stocks.GetAllForUniverseScreeningAsync(cancellationToken);
        var activeBefore = all.Count(s => s.IsActive);
        var deactivated = 0;
        var reactivated = 0;
        var staleSkipped = new List<string>();
        var updatedAt = DateTime.UtcNow;
        var today = VietnamMarketCalendar.TodayVietnam();

        foreach (var stock in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screen = StockUniverseFilter.ScreenQuality(stock.History, settings);

            if (screen.Passes)
            {
                if (!stock.IsActive)
                {
                    // Không khôi phục mã có history đóng băng/đứt đoạn — chỉ báo tính trên nến cũ sẽ sai.
                    // Những mã này cần Job 1 backfill lại đầy đủ (Job 2 warm sẽ tự làm liền history về sau).
                    if (StockUniverseFilter.IsHistoryStale(
                            stock.History, today, settings.VolumeLookbackSessions))
                    {
                        staleSkipped.Add(stock.Symbol);
                        continue;
                    }

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

        if (staleSkipped.Count > 0)
            logger.LogWarning(
                "Universe rescreen: {Count} mã đạt giá/thanh khoản nhưng history đóng băng/đứt đoạn — chưa khôi phục, cần chạy Job 1 backfill: {Symbols}",
                staleSkipped.Count,
                string.Join(", ", staleSkipped.Take(50)));

        return new UniverseRescreenResultDto(activeBefore, deactivated, reactivated, updatedAt);
    }
}
