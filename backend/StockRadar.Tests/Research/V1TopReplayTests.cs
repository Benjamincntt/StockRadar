using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;
using StockRadar.Domain.ValueObjects;
using Xunit;
using Xunit.Abstractions;

namespace StockRadar.Tests.Research;

/// <summary>
/// Công cụ nghiên cứu, không phải test hồi quy: phát lại cách DailyAnalysisRunner chọn Top V1
/// cho từng phiên quá khứ trên nến đã cắt tới phiên đó, ghi Top ra CSV để mô phỏng mua bán bằng Python.
/// Chỉ chạy khi đặt biến môi trường V1_REPLAY_DIR. Chi tiết: docs/features/v3-scenario-engine/research (PH-31).
/// </summary>
public sealed class V1TopReplayTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Đọc nến các mã (prod_stocks.txt), VNINDEX (vnindex_hist.json) và appsettings trong V1_REPLAY_DIR,
    /// chạy từ V1_REPLAY_FROM tới hết dữ liệu, ghi v1_replay_top.csv vào cùng thư mục.
    /// </summary>
    [Fact]
    [Trait("Category", "Research")]
    public void ReplayV1Top()
    {
        var dir = Environment.GetEnvironmentVariable("V1_REPLAY_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            return;

        var fromDate = DateOnly.Parse(Environment.GetEnvironmentVariable("V1_REPLAY_FROM") ?? "2023-01-01", CultureInfo.InvariantCulture);
        var toText = Environment.GetEnvironmentVariable("V1_REPLAY_TO");
        var toDate = string.IsNullOrWhiteSpace(toText) ? DateOnly.MaxValue : DateOnly.Parse(toText, CultureInfo.InvariantCulture);
        var stocksFile = Environment.GetEnvironmentVariable("V1_REPLAY_STOCKS") ?? Path.Combine(dir, "prod_stocks.txt");

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(dir, "appsettings.Production.json"), optional: true)
            .Build();
        var jobs = config.GetSection(MarketJobsOptions.SectionName).Get<MarketJobsOptions>() ?? new();
        var smOptions = config.GetSection(SmartMoneyOptions.SectionName).Get<SmartMoneyOptions>() ?? new();
        var runupOptions = config.GetSection(PriceRunupFilterOptions.SectionName).Get<PriceRunupFilterOptions>() ?? new();
        var rankerOptions = config.GetSection(OpportunityRankerOptions.SectionName).Get<OpportunityRankerOptions>() ?? new();
        var cfg = jobs.DailyAnalysis;
        var sm = smOptions.ToSettings();
        var runup = runupOptions.ToSettings();
        var minCloseStored = jobs.History.MinClosePriceVnd / 1000m;

        var signals = new SignalAnalyzer();
        var buyDecision = new BuyDecisionEngine(signals);
        var smartMoney = new SmartMoneyOpportunitySelector(signals, buyDecision);
        var regimeEngine = new SectorWaveRegimeEngine();
        var ranker = new OpportunityRankerService(new NoModelStore(), Options.Create(rankerOptions));
        var modelFile = Environment.GetEnvironmentVariable("V1_REPLAY_MODEL");
        if (!string.IsNullOrWhiteSpace(modelFile))
        {
            var model = JsonSerializer.Deserialize<OpportunityRankerModel>(File.ReadAllText(modelFile), JsonOptions);
            ranker.SetModel(model ?? OpportunityRankerModel.Untrained());
        }
        else
        {
            ranker.SetModel(OpportunityRankerModel.Untrained());
        }

        var all = LoadStocks(stocksFile);
        var indexBars = JsonSerializer.Deserialize<List<OhlcvBar>>(File.ReadAllText(Path.Combine(dir, "vnindex_hist.json")), JsonOptions)!
            .OrderBy(b => b.Date).ToList();
        var dateIndex = all.Select(s => s.History.Select((b, i) => (b.Date, i)).ToDictionary(x => x.Date, x => x.i)).ToList();

        var tradingDates = indexBars.Select(b => b.Date).Where(d => d >= fromDate && d <= toDate).ToList();
        var regimes = new Dictionary<string, SectorWaveRegimeState?>(StringComparer.OrdinalIgnoreCase);
        var csv = new StringBuilder("asof,rank,symbol,sector,buy_score,trade_state,legacy_rec,ml_prob,phase,candidates,universe\n");
        var watch = Stopwatch.StartNew();
        output.WriteLine($"{all.Count} mã, {tradingDates.Count} phiên, ML {(ranker.IsModelActive ? "bật" : "tắt (xếp theo PredictedHitPercent)")}");

        foreach (var asOf in tradingDates)
        {
            var universe = new List<Stock>();
            for (var k = 0; k < all.Count; k++)
            {
                if (!dateIndex[k].TryGetValue(asOf, out var idx))
                    continue;
                if (all[k].History[idx].Close <= minCloseStored)
                    continue;
                universe.Add(all[k] with { History = new HistorySlice(all[k].History, idx + 1) });
            }

            var indexUpTo = indexBars.Where(b => b.Date <= asOf).ToList();
            var change1 = signals.GetChangePercent(indexUpTo, 1);
            var change5 = signals.GetChangePercent(indexUpTo, 5);
            var index = new MarketIndex("VNINDEX", indexUpTo[^1].Close, change1, Math.Clamp(50 + (int)(change1 * 10), 0, 100),
                change1 > 0.5m ? MarketTrend.Uptrend : change1 < -0.5m ? MarketTrend.Downtrend : MarketTrend.Sideway,
                change5, indexUpTo);

            var context = smartMoney.BuildContext(universe, index, runup, sm, null, null);

            // Sóng ngành kế thừa xuyên phiên, giữ trong bộ nhớ thay cho bảng SectorWaveRegimes.
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sector, snapshot) in context.SectorSnapshots)
            {
                regimes.TryGetValue(sector, out var previous);
                var next = regimeEngine.Advance(sector, previous, snapshot, asOf, sm.SectorWaveThresholds);
                regimes[sector] = next;
                if (next.IsActive)
                    active.Add(sector);
            }
            context = context with { ActiveSectorRegimes = active };

            var candidates = new List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation Decision)>();
            foreach (var stock in universe)
            {
                if (string.IsNullOrWhiteSpace(stock.Sector))
                    continue;
                var latestBar = stock.History[^1];
                if (latestBar.Volume <= 0)
                    continue;

                var decision = buyDecision.Evaluate(stock, context);
                if (decision.VolumeRatio < cfg.MinVolumeRatioForTop)
                    continue;
                if (IndicatorMath.AverageTurnoverValue(stock.History, 20) < cfg.MinGiaTriGiaoDichTB)
                    continue;

                var entryPoint = decision.Entry;
                if (entryPoint.BaseLow > 0 && entryPoint.BaseHigh > 0
                    && (latestBar.Close < entryPoint.BaseLow * 0.5m || latestBar.Close > entryPoint.BaseHigh * 2.0m))
                    continue;

                var eval = smartMoney.Evaluate(stock, context, decision);
                if (!smartMoney.PassesFilter(eval, sm))
                    continue;
                candidates.Add((stock, eval, decision));
            }

            var ordered = candidates
                .Select(c =>
                {
                    var tradeState = TradeStateResolver.Resolve(c.Decision.Entry, c.Decision.GateFailure, c.Decision.BuyScore,
                        new TradeStateListContext(true));
                    var (atrPct, distMa20) = ComputeAtrAndDistMa20(c.Stock.History);
                    var rankInput = OpportunityRankInput.FromEvaluation(c.Decision.BuyScore, c.Decision.PredictedHitPercent,
                        c.Decision.SectorWave.WaveRank, c.Decision.RelativeStrength5d, c.Decision.VolumeRatio, tradeState.State,
                        c.Decision.SetupDna, context.MarketPhase, atrPct, distMa20);
                    var mlProb = ranker.PredictWinProbability(rankInput);
                    return new Candidate(c.Stock, c.Eval, c.Decision, tradeState, mlProb, mlProb + GetSectorPriorityBonus(c.Stock.Sector) / 100m);
                })
                .OrderByDescending(x => x.RankedScore)
                .ThenByDescending(x => x.Eval.Score)
                .ThenByDescending(x => (int)x.Eval.SectorWave.Wave)
                .ThenByDescending(x => x.Eval.RelativeStrength5d)
                .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
                .Where(x => PassesTopHygiene(x.Decision, x.TradeState, context.MarketPhase))
                .ToList();
            ordered = ApplySectorCap(ordered, cfg.MaxPerSector);
            if (cfg.MaxResults > 0)
                ordered = ordered.Take(cfg.MaxResults).ToList();

            for (var r = 0; r < ordered.Count; r++)
            {
                var x = ordered[r];
                var legacy = TradeStateLabels.ToLegacyRecommendation(x.TradeState.State, x.Decision.BuyScore);
                csv.Append(CultureInfo.InvariantCulture,
                    $"{asOf:yyyy-MM-dd},{r + 1},{x.Stock.Symbol},\"{x.Stock.Sector}\",{x.Decision.BuyScore},{x.TradeState.State},{legacy},{x.MlProb},{context.MarketPhase},{candidates.Count},{universe.Count}\n");
            }

            if (asOf.Day <= 7 && asOf.DayOfWeek == DayOfWeek.Monday)
                output.WriteLine($"{asOf:yyyy-MM-dd} universe {universe.Count}, ứng viên {candidates.Count}, Top {ordered.Count}, {watch.Elapsed:mm\\:ss}");
        }

        var outFile = Path.Combine(dir, Environment.GetEnvironmentVariable("V1_REPLAY_OUT") ?? "v1_replay_top.csv");
        File.WriteAllText(outFile, csv.ToString(), new UTF8Encoding(false));
        output.WriteLine($"Ghi {outFile} sau {watch.Elapsed:mm\\:ss}");
    }

    /// <summary>Đọc prod_stocks.txt (Symbol|Exchange|Sector|IsActive|HistoryJson), giữ nến từ 2020 để đủ 250 phiên trước 2023.</summary>
    /// <param name="path">Đường dẫn file xuất từ bảng Stocks.</param>
    /// <returns>Danh sách mã có nến, lịch sử sắp theo ngày tăng dần.</returns>
    private static List<Stock> LoadStocks(string path)
    {
        var list = new List<Stock>();
        var cutoff = new DateOnly(2020, 1, 1);
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split('|', 5);
            if (parts.Length < 5 || !parts[4].StartsWith('['))
                continue;
            var bars = JsonSerializer.Deserialize<List<OhlcvBar>>(parts[4], JsonOptions);
            if (bars is null || bars.Count == 0)
                continue;
            var history = bars.Where(b => b.Date >= cutoff).OrderBy(b => b.Date).ToList();
            list.Add(new Stock(parts[0], parts[0], parts[2], history, Exchange: parts[1]));
        }
        return list;
    }

    /// <summary>Chép từ DailyAnalysisRunner.GetSectorPriorityBonus: +10 ngành trọng yếu, +5 ngành phụ trợ.</summary>
    /// <param name="sector">Tên ngành theo SectorCatalog.</param>
    /// <returns>Điểm cộng xếp hạng.</returns>
    private static decimal GetSectorPriorityBonus(string? sector)
    {
        string[] keySectors = ["Ngân hàng", "Dịch vụ tài chính", "Thép", "Bất động sản", "Dầu khí"];
        string[] supportSectors = ["Xây dựng và Vật liệu", "Vật liệu xây dựng", "Bán lẻ", "Thực phẩm và đồ uống"];
        var name = sector ?? "";
        return keySectors.Contains(name, StringComparer.OrdinalIgnoreCase) ? 10m
            : supportSectors.Contains(name, StringComparer.OrdinalIgnoreCase) ? 5m
            : 0m;
    }

    /// <summary>Chép từ DailyAnalysisRunner.ApplySectorCap: giữ tối đa maxPerSector mã mỗi ngành theo thứ tự xếp.</summary>
    /// <param name="ordered">Ứng viên đã xếp hạng.</param>
    /// <param name="maxPerSector">Số mã tối đa mỗi ngành, 0 là không giới hạn.</param>
    /// <returns>Danh sách sau khi giới hạn ngành.</returns>
    private static List<Candidate> ApplySectorCap(List<Candidate> ordered, int maxPerSector)
    {
        if (maxPerSector <= 0)
            return ordered;
        var sectorCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<Candidate>();
        foreach (var item in ordered)
        {
            var sector = item.Stock.Sector ?? "Khác";
            sectorCount.TryGetValue(sector, out var count);
            if (count >= maxPerSector)
                continue;
            sectorCount[sector] = count + 1;
            kept.Add(item);
        }
        return kept;
    }

    /// <summary>Chép từ DailyAnalysisRunner.PassesTopHygiene: bỏ Avoid; breakout ngoài pha Favorable phải Actionable, trừ RS Leader.</summary>
    /// <param name="decision">Kết quả chấm điểm mua.</param>
    /// <param name="tradeState">Trạng thái giao dịch đã suy ra.</param>
    /// <param name="phase">Pha thị trường của phiên.</param>
    /// <returns>true nếu được giữ trong Top.</returns>
    private static bool PassesTopHygiene(BuyDecisionEvaluation decision, TradeStateResult tradeState, MarketWyckoffPhase phase)
    {
        if (tradeState.State == StockTradeState.Avoid)
            return false;
        if (decision.IsRsLeader)
            return true;
        var dna = decision.SetupDna ?? "";
        var isBreakout = decision.Entry.Type == EntryPointType.Breakout
            || dna.StartsWith("Breakout", StringComparison.OrdinalIgnoreCase)
            || dna.Contains("Phá vỡ", StringComparison.OrdinalIgnoreCase);
        if (!isBreakout)
            return true;
        return phase == MarketWyckoffPhase.Favorable || tradeState.State == StockTradeState.Actionable;
    }

    /// <summary>Chép từ DailyAnalysisRunner.ComputeAtrAndDistMa20: ATR14 theo % giá và khoảng cách tới MA20 tại phiên cuối.</summary>
    /// <param name="history">Lịch sử nến đã cắt tới phiên đang xét.</param>
    /// <returns>ATR% và khoảng cách MA20 theo %.</returns>
    private static (decimal AtrPct, decimal DistMa20) ComputeAtrAndDistMa20(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 5)
            return (0m, 0m);
        var idx = history.Count - 1;
        var bar = history[idx];
        var atr = IndicatorMath.Atr(history, 14);
        var atrPct = bar.Close > 0 ? atr / bar.Close * 100m : 0m;
        var distMa20 = 0m;
        if (Math.Min(20, idx + 1) >= 5)
        {
            var ma20 = IndicatorMath.SmaAt(history, idx, 20);
            if (ma20 > 0)
                distMa20 = (bar.Close - ma20) / ma20 * 100m;
        }
        return (atrPct, distMa20);
    }

    private sealed record Candidate(
        Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation Decision, TradeStateResult TradeState, decimal MlProb, decimal RankedScore);

    /// <summary>Phần đầu của một danh sách nến, không chép dữ liệu.</summary>
    private sealed class HistorySlice(IReadOnlyList<OhlcvBar> source, int count) : IReadOnlyList<OhlcvBar>
    {
        public OhlcvBar this[int index] =>
            (uint)index < (uint)count ? source[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public int Count => count;

        public IEnumerator<OhlcvBar> GetEnumerator()
        {
            for (var i = 0; i < count; i++)
                yield return source[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Kho model rỗng: harness tự đặt model qua SetModel.</summary>
    private sealed class NoModelStore : IOpportunityRankerModelStore
    {
        public Task<OpportunityRankerModel> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OpportunityRankerModel.Untrained());

        public Task SaveAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveVersionOnlyAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<OpportunityRankerModelVersionInfo>> ListVersionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OpportunityRankerModelVersionInfo>>([]);

        public Task<bool> RevertToVersionAsync(string versionFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
