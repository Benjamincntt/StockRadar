using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>Đầu vào đo shadow: 1 tín hiệu production đã lưu + OHLCV forward của mã.</summary>
public sealed record ReversalBounceShadowInput(
    string Symbol,
    DateOnly SignalDate,
    MarketRegime Regime,
    string Exchange,
    IReadOnlyList<OhlcvBar> Bars,
    int SignalIndex,
    ReversalBounceTradePlan Plan,
    decimal AtrPercent);

/// <summary>Kết quả đo 1 tín hiệu (Measured / Pending / GapCancelled).</summary>
public sealed record ReversalBounceShadowTrade(
    string Symbol,
    DateOnly SignalDate,
    string Status,
    string ExitReason,
    int SessionsToExit,
    decimal ReturnPercentNet,
    decimal MaxFavorablePercent,
    decimal MaxAdversePercent,
    string Bucket,
    string Regime);

/// <summary>Thống kê theo regime.</summary>
public sealed record ReversalBounceShadowRegimeStat(
    string Regime,
    int Measured,
    int Win,
    int Flat,
    int Lose,
    decimal WinRatePercent,
    decimal AvgReturnPercentNet);

/// <summary>Báo cáo shadow tổng hợp cho khoảng [From, To].</summary>
public sealed record ReversalBounceShadowSummary(
    DateOnly From,
    DateOnly To,
    int TotalActionable,
    int Measured,
    int Pending,
    int GapCancelled,
    int Win,
    int Flat,
    int Lose,
    decimal WinRatePercent,
    decimal AvgReturnPercentNet,
    decimal AvgMfePercent,
    decimal AvgMaePercent,
    IReadOnlyList<ReversalBounceShadowRegimeStat> ByRegime,
    IReadOnlyList<ReversalBounceShadowTrade> Trades);

/// <summary>
/// Đo hiệu quả "shadow mode" (Phase 1): với mỗi tín hiệu production đã lưu, mô phỏng outcome bằng
/// <see cref="ReversalBounceFillSimulator"/> trên OHLCV forward. Thuần &amp; deterministic để test.
/// Win ≥ +1% / Flat / Lose ≤ -0.5% (net sau phí, khớp §11.3).
/// </summary>
public static class ReversalBounceShadowEvaluator
{
    private const string StatusMeasured = "Measured";
    private const string StatusPending = "Pending";
    private const string StatusGapCancelled = "GapCancelled";

    public const string BucketWin = "Win";
    public const string BucketFlat = "Flat";
    public const string BucketLose = "Lose";
    private const string BucketNone = "-";

    public static ReversalBounceShadowSummary Evaluate(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<ReversalBounceShadowInput> inputs,
        decimal gapCancelAtrMultiple,
        ReversalBounceTradeSettings trade,
        bool allowDefensiveEarlyExit)
    {
        var trades = new List<ReversalBounceShadowTrade>(inputs.Count);

        foreach (var input in inputs)
        {
            // Chưa có phiên T+1 → chưa vào lệnh được → Pending.
            if (input.SignalIndex < 0 || input.SignalIndex + 1 >= input.Bars.Count)
            {
                trades.Add(Trade(input, StatusPending, ReversalBounceExitReasons.OpenEnded, 0, 0m, 0m, 0m, BucketNone));
                continue;
            }

            var fill = ReversalBounceFillSimulator.Simulate(
                input.Bars, input.SignalIndex, input.Exchange, input.Plan, input.AtrPercent,
                gapCancelAtrMultiple, trade, allowDefensiveEarlyExit);

            var (status, bucket) = fill.ExitReason switch
            {
                ReversalBounceExitReasons.GapCancelled => (StatusGapCancelled, BucketNone),
                ReversalBounceExitReasons.OpenEnded => (StatusPending, BucketNone), // hết dữ liệu, chưa chốt
                _ => (StatusMeasured, Bucket(fill.ReturnPercentNet)),
            };

            trades.Add(Trade(
                input, status, fill.ExitReason, fill.SessionsToExit,
                fill.ReturnPercentNet, fill.MaxFavorablePercent, fill.MaxAdversePercent, bucket));
        }

        var measured = trades.Where(t => t.Status == StatusMeasured).ToList();
        var win = measured.Count(t => t.Bucket == BucketWin);
        var lose = measured.Count(t => t.Bucket == BucketLose);
        var flat = measured.Count - win - lose;

        var byRegime = measured
            .GroupBy(t => t.Regime)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var w = g.Count(t => t.Bucket == BucketWin);
                var l = g.Count(t => t.Bucket == BucketLose);
                return new ReversalBounceShadowRegimeStat(
                    Regime: g.Key,
                    Measured: g.Count(),
                    Win: w,
                    Flat: g.Count() - w - l,
                    Lose: l,
                    WinRatePercent: Rate(w, g.Count()),
                    AvgReturnPercentNet: Avg(g.Select(t => t.ReturnPercentNet)));
            })
            .ToList();

        return new ReversalBounceShadowSummary(
            From: from,
            To: to,
            TotalActionable: inputs.Count,
            Measured: measured.Count,
            Pending: trades.Count(t => t.Status == StatusPending),
            GapCancelled: trades.Count(t => t.Status == StatusGapCancelled),
            Win: win,
            Flat: flat,
            Lose: lose,
            WinRatePercent: Rate(win, measured.Count),
            AvgReturnPercentNet: Avg(measured.Select(t => t.ReturnPercentNet)),
            AvgMfePercent: Avg(measured.Select(t => t.MaxFavorablePercent)),
            AvgMaePercent: Avg(measured.Select(t => t.MaxAdversePercent)),
            ByRegime: byRegime,
            Trades: trades);
    }

    private static string Bucket(decimal net) =>
        net >= 1m ? BucketWin : net <= -0.5m ? BucketLose : BucketFlat;

    private static ReversalBounceShadowTrade Trade(
        ReversalBounceShadowInput input, string status, string exitReason, int sessions,
        decimal net, decimal mfe, decimal mae, string bucket) =>
        new(
            Symbol: input.Symbol,
            SignalDate: input.SignalDate,
            Status: status,
            ExitReason: exitReason,
            SessionsToExit: sessions,
            ReturnPercentNet: net,
            MaxFavorablePercent: mfe,
            MaxAdversePercent: mae,
            Bucket: bucket,
            Regime: input.Regime.ToString());

    private static decimal Rate(int part, int total) =>
        total == 0 ? 0m : Math.Round(part / (decimal)total * 100m, 2);

    private static decimal Avg(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0m : Math.Round(list.Average(), 2);
    }
}
