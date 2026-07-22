using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Quét universe tìm ứng viên counter-trend ("sóng hồi") cho một phiên: analyzer (Domain) → decision
/// engine → snapshot idempotent. Chạy cuối daily pipeline, SAU <see cref="MarketBreadthRunner"/>
/// (cần regime hôm nay). Không đụng các job pro-trend hiện có.
/// </summary>
internal sealed class ReversalBounceAnalysisRunner(
    ApplicationDbContext db,
    IMarketBreadthSnapshotRepository breadthRepo,
    IReversalBounceAnalyzer analyzer,
    ICounterTrendDecisionEngine decision,
    IReversalCandidateSnapshotRepository snapshotRepo,
    IOptions<ReversalBounceOptions> options,
    ILogger<ReversalBounceAnalysisRunner> logger)
    : IReversalBounceAnalysisService
{
    public async Task<ReversalBounceAnalysisResult> RunAsync(
        DateOnly forTradingDate,
        IReadOnlyList<Stock> universe,
        CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var settings = opt.ToSettings();
        var runBatchId = Guid.NewGuid();
        var parametersHash = ComputeParametersHash(settings);

        if (!opt.Enabled)
            return new ReversalBounceAnalysisResult(runBatchId, forTradingDate, 0, 0, 0);

        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var breadth = await breadthRepo.GetForDateAsync(forTradingDate, cancellationToken)
                      ?? await breadthRepo.GetPreviousAsync(forTradingDate, cancellationToken);
        var regime = breadth?.Regime ?? MarketRegime.Normal;

        var rsPercentiles = BuildRsPercentiles(universe, opt.MinHistoryDays);

        var candidates = new List<(ReversalCandidateSnapshot Snapshot, bool Eligible)>();
        var scanned = 0;

        foreach (var stock in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stock.IsActive || stock.TradingRestricted)
                continue;
            if (stock.History.Count < opt.MinHistoryDays)
                continue;
            if (AverageVolume(stock.History) < opt.MinAvgDailyVolume)
                continue;

            scanned++;

            var rsPct = rsPercentiles.GetValueOrDefault(stock.Symbol, 50m);
            var analysis = analyzer.Analyze(stock, indexHistory, regime, rsPct, forTradingDate, settings);
            var setup = analysis.Setup;
            if (setup.Stage == ReversalBounceStage.None)
                continue;

            var priorCount = await snapshotRepo.CountSameSetupPriorAsync(
                stock.Symbol, setup.SetupId, settings.StrategyVersion, forTradingDate, cancellationToken);
            var recoveryAttempt = priorCount + 1;

            var signal = decision.Decide(setup, analysis.Features, settings);
            var eligible = signal.TradePlan is not null;

            candidates.Add((
                new ReversalCandidateSnapshot(
                    TradingDate: forTradingDate,
                    Symbol: setup.Symbol,
                    Stage: setup.Stage,
                    SetupId: setup.SetupId,
                    CapitulationDate: setup.CapitulationDate,
                    CapitulationLow: setup.CapitulationLow,
                    CapitulationClose: setup.CapitulationClose,
                    RecoveryAttemptCount: recoveryAttempt,
                    ComponentScores: setup.ComponentScores,
                    TotalScore: setup.TotalScore,
                    MarketRegime: setup.MarketRegime,
                    IsActionable: false,
                    TradePlan: signal.TradePlan,
                    StrategyVersion: settings.StrategyVersion,
                    AlgorithmParametersHash: parametersHash,
                    SchemaVersion: settings.SchemaVersion,
                    RunBatchId: runBatchId,
                    Reasons: setup.Reasons,
                    CreatedAtUtc: DateTime.UtcNow),
                eligible));
        }

        // Top-N actionable trong ngày (giữ audit trail cho phần còn lại → IsActionable=false).
        var actionableSetupIds = candidates
            .Where(c => c.Eligible)
            .OrderByDescending(c => c.Snapshot.TotalScore)
            .ThenBy(c => c.Snapshot.Symbol)
            .Take(settings.Trade.MaxSignalsPerDay)
            .Select(c => c.Snapshot.SetupId)
            .ToHashSet();

        var written = 0;
        foreach (var (snapshot, _) in candidates)
        {
            var final = snapshot with { IsActionable = actionableSetupIds.Contains(snapshot.SetupId) };
            await snapshotRepo.UpsertAsync(final, cancellationToken);
            written++;
        }

        logger.LogInformation(
            "ReversalBounce {Date}: quét {Scanned}, snapshot {Written}, actionable {Actionable} (regime {Regime}).",
            forTradingDate, scanned, written, actionableSetupIds.Count, regime);

        return new ReversalBounceAnalysisResult(
            runBatchId, forTradingDate, scanned, written, actionableSetupIds.Count);
    }

    public async Task<ReversalCandidateSnapshot?> AnalyzeSymbolLiveAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var settings = opt.ToSettings();
        var sym = symbol.Trim().ToUpperInvariant();
        var forDate = TradingCalendar.GetActiveOpportunityDate();

        var entity = await db.Stocks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
            return null;

        var stock = EntityMapper.ToDomain(entity);
        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var breadth = await breadthRepo.GetForDateAsync(forDate, cancellationToken)
                      ?? await breadthRepo.GetPreviousAsync(forDate, cancellationToken);
        var regime = breadth?.Regime ?? MarketRegime.Normal;

        // RS percentile đơn mã: so với universe active (cùng công thức runner batch).
        var universe = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && !s.TradingRestricted)
            .ToListAsync(cancellationToken);
        var domainUniverse = universe.Select(EntityMapper.ToDomain).ToList();
        var rsPct = BuildRsPercentiles(domainUniverse, opt.MinHistoryDays)
            .GetValueOrDefault(sym, 50m);

        var analysis = analyzer.Analyze(stock, indexHistory, regime, rsPct, forDate, settings);
        var setup = analysis.Setup;
        var priorCount = setup.Stage == ReversalBounceStage.None
            ? 0
            : await snapshotRepo.CountSameSetupPriorAsync(
                sym, setup.SetupId, settings.StrategyVersion, forDate, cancellationToken);
        var signal = decision.Decide(setup, analysis.Features, settings);

        return new ReversalCandidateSnapshot(
            TradingDate: forDate,
            Symbol: setup.Symbol,
            Stage: setup.Stage,
            SetupId: setup.SetupId,
            CapitulationDate: setup.CapitulationDate,
            CapitulationLow: setup.CapitulationLow,
            CapitulationClose: setup.CapitulationClose,
            RecoveryAttemptCount: priorCount + 1,
            ComponentScores: setup.ComponentScores,
            TotalScore: setup.TotalScore,
            MarketRegime: setup.MarketRegime,
            IsActionable: signal.TradePlan is not null,
            TradePlan: signal.TradePlan,
            StrategyVersion: settings.StrategyVersion,
            AlgorithmParametersHash: ComputeParametersHash(settings),
            SchemaVersion: settings.SchemaVersion,
            RunBatchId: Guid.Empty,
            Reasons: setup.Reasons,
            CreatedAtUtc: DateTime.UtcNow);
    }

    /// <summary>RS percentile theo return 20 phiên trên toàn universe (0..100).</summary>
    private static Dictionary<string, decimal> BuildRsPercentiles(IReadOnlyList<Stock> universe, int minHistory)
    {
        var returns = new List<(string Symbol, decimal Return)>();
        foreach (var s in universe)
        {
            if (s.History.Count < Math.Max(21, minHistory))
                continue;
            var last = s.History[^1].Close;
            var prior = s.History[^Math.Min(21, s.History.Count)].Close;
            if (prior <= 0)
                continue;
            returns.Add((s.Symbol, (last - prior) / prior));
        }

        var map = new Dictionary<string, decimal>(returns.Count);
        if (returns.Count == 0)
            return map;

        var ordered = returns.OrderBy(r => r.Return).ToList();
        var n = ordered.Count;
        for (var i = 0; i < n; i++)
            map[ordered[i].Symbol] = n == 1 ? 100m : Math.Round(i / (decimal)(n - 1) * 100m, 2);
        return map;
    }

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson))
            return [];
        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, EntityMapper.JsonOptions) ?? [];
    }

    private static decimal AverageVolume(IReadOnlyList<OhlcvBar> history)
    {
        var count = Math.Min(20, history.Count);
        return count == 0 ? 0m : history.TakeLast(count).Average(b => (decimal)b.Volume);
    }

    private static string ComputeParametersHash(ReversalBounceSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
