using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class KbsMarketSyncRunner(
    KbsPriceBoardClient kbs,
    KbsIndexClient indexClient,
    IMarketSyncService sync,
    IJobStockRepository stocks,
    ILogger<KbsMarketSyncRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        var stockMap = all.ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);
        var symbols = stockMap.Keys.ToList();
        if (symbols.Count == 0)
        {
            logger.LogWarning("DB trống — không sync KBS. Chạy Job 1 (backfill) trước.");
            return;
        }

        var fetchSymbols = symbols.Where(s => !s.Equals("VNINDEX", StringComparison.OrdinalIgnoreCase)).ToList();
        var board = await kbs.FetchAsync(fetchSymbols, cancellationToken);
        if (board.Count == 0 && fetchSymbols.Count > 0)
        {
            logger.LogWarning("KBS không trả dữ liệu cho {Count} mã.", fetchSymbols.Count);
        }

        var quotes = board
            .Where(r => r.Symbol is not ("VNINDEX" or "VN-INDEX"))
            .Select(r =>
            {
                stockMap.TryGetValue(r.Symbol, out var stock);
                return new StockQuoteSyncDto(
                    r.Symbol,
                    null,
                    r.Open,
                    r.High,
                    r.Low,
                    r.Close,
                    r.SessionVolume,
                    r.ChangePercent,
                    stock?.Sector);
            })
            .ToList();

        MarketIndexSyncDto? index = null;
        var vnIndex = await indexClient.FetchVnIndexAsync(cancellationToken);
        if (vnIndex is not null)
            index = new MarketIndexSyncDto("VNINDEX", vnIndex.Price, vnIndex.ChangePercent);

        if (quotes.Count == 0 && index is null)
            return;

        var result = await sync.ApplyAsync(new MarketSyncRequest(index, quotes), cancellationToken);
        logger.LogInformation(
            "KBS sync: stocks={Stocks}, index={Index}",
            result.StocksUpdated,
            result.IndexUpdated);
    }
}
