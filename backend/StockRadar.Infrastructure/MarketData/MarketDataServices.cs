using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class DatabaseMarketIndexProvider(
    EfMarketIndexRepository repository,
    KbsIndexClient kbsIndex,
    IMarketDataWriter writer) : IMarketIndexProvider, IJobMarketIndexProvider
{
    public async Task<MarketIndex> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var index = await repository.GetAsync("VNINDEX", cancellationToken);
        if (index is not null && index.Price > 0)
            return index;

        var live = await kbsIndex.FetchVnIndexAsync(cancellationToken);
        if (live is not null && live.Price > 0)
        {
            var dto = new Application.DTOs.MarketIndexSyncDto("VNINDEX", live.Price, live.ChangePercent);
            await writer.UpsertIndexAsync(dto, cancellationToken);
            return await repository.GetAsync("VNINDEX", cancellationToken)
                   ?? new MarketIndex("VNINDEX", live.Price, live.ChangePercent, Score(live.ChangePercent), Trend(live.ChangePercent));
        }

        throw new AppException(
            "Chưa có VNINDEX",
            "Chưa có dữ liệu VNINDEX — chạy Job 2 hoặc sync KBS trước.",
            503);
    }

    private static int Score(decimal changePercent) =>
        Math.Clamp(50 + (int)(changePercent * 10), 0, 100);

    private static Domain.Enums.MarketTrend Trend(decimal changePercent) => changePercent switch
    {
        > 0.5m => Domain.Enums.MarketTrend.Uptrend,
        < -0.5m => Domain.Enums.MarketTrend.Downtrend,
        _ => Domain.Enums.MarketTrend.Sideway
    };
}
