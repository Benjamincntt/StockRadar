using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services.ReversalBounce;

public interface IMarketBreadthAnalyzer
{
    MarketBreadthSnapshot Analyze(
        IReadOnlyList<Stock> universe,
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly tradingDate);
}

/// <summary>
/// Tính độ rộng thị trường từ OHLCV toàn universe + VN-Index history. Stateless, pure.
/// Regime để mặc định <see cref="MarketRegime.Normal"/> — <see cref="MarketRegimeClassifier"/> sẽ điền sau.
/// </summary>
/// <remarks>
/// Floor/Ceiling hiện dùng <b>proxy theo %thay đổi phiên</b> so biên độ sàn giao dịch
/// (chưa có ReferencePrice/FloorPrice — xem audit 0A.2). Khi có FloorPrice sẽ thay bằng so giá thực.
/// </remarks>
public sealed class MarketBreadthAnalyzer : IMarketBreadthAnalyzer
{
    private const int Ma20Window = 20;
    private const int Ma50Window = 50;
    private const int NewLowWindow = 20;
    private const int DrawdownWindow = 60;

    public MarketBreadthSnapshot Analyze(
        IReadOnlyList<Stock> universe,
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly tradingDate)
    {
        var aboveMa20 = 0;
        var aboveMa50 = 0;
        var ma50Eligible = 0;
        var newLow = 0;
        var up = 0;
        var down = 0;
        var floor = 0;
        var ceiling = 0;
        var eligible = 0;
        var returns = new List<decimal>();
        var turnovers = new List<decimal>();

        foreach (var stock in universe)
        {
            if (!stock.IsActive || stock.TradingRestricted)
                continue;

            var history = stock.History;
            if (history.Count < Ma20Window + 1)
                continue;

            eligible++;
            var latest = history[^1];
            var prevClose = history[^2].Close;

            if (latest.Close > Average(history, Ma20Window))
                aboveMa20++;

            if (history.Count >= Ma50Window)
            {
                ma50Eligible++;
                if (latest.Close > Average(history, Ma50Window))
                    aboveMa50++;
            }

            var low20 = MinLow(history, NewLowWindow);
            if (latest.Low <= low20)
                newLow++;

            var change = prevClose > 0
                ? (latest.Close - prevClose) / prevClose * 100m
                : 0m;
            returns.Add(change);
            if (change > 0) up++;
            else if (change < 0) down++;

            var band = ExchangeBandPercent(stock.Exchange);
            if (change <= -(band - 0.3m)) floor++;
            else if (change >= band - 0.3m) ceiling++;

            turnovers.Add(latest.Close * latest.Volume);
        }

        var (drawdown, distanceToMa20, indexAboveMa20, reclaimed) = AnalyzeIndex(indexHistory);

        return new MarketBreadthSnapshot(
            TradingDate: tradingDate,
            UniverseCount: eligible,
            PctAboveMa20: Pct(aboveMa20, eligible),
            PctAboveMa50: Pct(aboveMa50, ma50Eligible),
            PctNewLow20: Pct(newLow, eligible),
            PctUp: Pct(up, eligible),
            PctDown: Pct(down, eligible),
            FloorCount: floor,
            CeilingCount: ceiling,
            MedianReturnPercent: Median(returns),
            MedianTurnover: Median(turnovers),
            VnIndexDrawdownPercent: drawdown,
            VnIndexDistanceToMa20Percent: distanceToMa20,
            VnIndexAboveMa20: indexAboveMa20,
            VnIndexReclaimedMa20: reclaimed,
            Regime: MarketRegime.Normal,
            ImproveStreak: 0);
    }

    private static (decimal Drawdown, decimal DistanceToMa20, bool AboveMa20, bool Reclaimed) AnalyzeIndex(
        IReadOnlyList<OhlcvBar> indexHistory)
    {
        if (indexHistory.Count < Ma20Window + 1)
            return (0m, 0m, true, false);

        var close = indexHistory[^1].Close;
        var ma20 = Average(indexHistory, Ma20Window);
        var aboveMa20 = close > ma20;
        var distanceToMa20 = ma20 > 0 ? (close - ma20) / ma20 * 100m : 0m;

        var highWindow = Math.Min(DrawdownWindow, indexHistory.Count);
        var rollingHigh = indexHistory.TakeLast(highWindow).Max(b => b.Close);
        var drawdown = rollingHigh > 0 ? (close - rollingHigh) / rollingHigh * 100m : 0m;

        // Reclaim = phiên trước dưới MA20, phiên này vượt lên trên.
        var prevBars = indexHistory.Take(indexHistory.Count - 1).ToList();
        var prevAboveMa20 = prevBars[^1].Close > Average(prevBars, Ma20Window);
        var reclaimed = aboveMa20 && !prevAboveMa20;

        return (
            Math.Round(drawdown, 2),
            Math.Round(distanceToMa20, 2),
            aboveMa20,
            reclaimed);
    }

    /// <summary>Biên độ dao động (±%) theo sàn — proxy để đếm mã sàn/trần khi chưa có FloorPrice.</summary>
    private static decimal ExchangeBandPercent(string exchange)
    {
        var ex = exchange?.Trim().ToUpperInvariant() ?? "";
        if (ex.Contains("HNX")) return 10m;
        if (ex.Contains("UPCOM") || ex.Contains("UPCM") || ex.Contains("UPC")) return 15m;
        return 7m; // HOSE / HSX / mặc định
    }

    private static decimal Average(IReadOnlyList<OhlcvBar> history, int window)
    {
        var count = Math.Min(window, history.Count);
        return history.TakeLast(count).Average(b => b.Close);
    }

    private static decimal MinLow(IReadOnlyList<OhlcvBar> history, int window)
    {
        var count = Math.Min(window, history.Count);
        return history.TakeLast(count).Min(b => b.Low);
    }

    private static decimal Pct(int part, int total) =>
        total <= 0 ? 0m : Math.Round(part / (decimal)total * 100m, 2);

    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0)
            return 0m;

        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1
            ? Math.Round(values[mid], 2)
            : Math.Round((values[mid - 1] + values[mid]) / 2m, 2);
    }
}
