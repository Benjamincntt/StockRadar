using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Services.ReversalBounce;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfReversalCandidateSnapshotRepository(ApplicationDbContext db)
    : IReversalCandidateSnapshotRepository
{
    public async Task UpsertAsync(ReversalCandidateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await db.ReversalCandidateSnapshots.FirstOrDefaultAsync(
            s => s.TradingDate == snapshot.TradingDate
                 && s.Symbol == snapshot.Symbol
                 && s.StrategyVersion == snapshot.StrategyVersion
                 && s.SetupId == snapshot.SetupId,
            cancellationToken);

        if (existing is null)
        {
            existing = new ReversalCandidateSnapshotEntity
            {
                Id = Guid.NewGuid(),
                TradingDate = snapshot.TradingDate,
                Symbol = snapshot.Symbol,
                SetupId = snapshot.SetupId,
                StrategyVersion = snapshot.StrategyVersion,
                CreatedAtUtc = snapshot.CreatedAtUtc == default ? DateTime.UtcNow : snapshot.CreatedAtUtc,
            };
            db.ReversalCandidateSnapshots.Add(existing);
        }

        existing.Stage = snapshot.Stage.ToString();
        existing.CapitulationDate = snapshot.CapitulationDate;
        existing.CapitulationLow = snapshot.CapitulationLow;
        existing.CapitulationClose = snapshot.CapitulationClose;
        existing.RecoveryAttemptCount = snapshot.RecoveryAttemptCount;

        existing.ScoreCapitulation = snapshot.ComponentScores.Capitulation;
        existing.ScoreStabilization = snapshot.ComponentScores.Stabilization;
        existing.ScoreDemand = snapshot.ComponentScores.Demand;
        existing.ScoreRelativeStrength = snapshot.ComponentScores.RelativeStrength;
        existing.ScoreLiquidity = snapshot.ComponentScores.Liquidity;
        existing.ScoreRiskPenalty = snapshot.ComponentScores.RiskPenalty;
        existing.TotalScore = snapshot.TotalScore;

        existing.MarketRegime = snapshot.MarketRegime.ToString();
        existing.IsActionable = snapshot.IsActionable;

        existing.EntryReference = snapshot.TradePlan?.EntryReference;
        existing.MaxEntryPrice = snapshot.TradePlan?.MaxEntryPrice;
        existing.InvalidationPrice = snapshot.TradePlan?.InvalidationPrice;
        existing.FirstTarget = snapshot.TradePlan?.FirstTarget;
        existing.RewardToRisk = snapshot.TradePlan?.RewardToRisk;
        existing.PositionFactor = snapshot.TradePlan?.PositionFactor;
        existing.TimeStopSessions = snapshot.TradePlan?.TimeStopSessions;
        existing.RiskWarningsJson = JsonSerializer.Serialize(
            snapshot.TradePlan?.RiskWarnings ?? [], EntityMapper.JsonOptions);

        existing.AlgorithmParametersHash = snapshot.AlgorithmParametersHash;
        existing.SchemaVersion = snapshot.SchemaVersion;
        existing.RunBatchId = snapshot.RunBatchId;
        existing.ReasonsJson = JsonSerializer.Serialize(snapshot.Reasons, EntityMapper.JsonOptions);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReversalCandidateSnapshot>> GetForDateAsync(
        DateOnly tradingDate, bool? actionableOnly = null, CancellationToken cancellationToken = default)
    {
        var q = db.ReversalCandidateSnapshots.AsNoTracking().Where(s => s.TradingDate == tradingDate);
        if (actionableOnly == true) q = q.Where(s => s.IsActionable);
        else if (actionableOnly == false) q = q.Where(s => !s.IsActionable);

        var rows = await q
            .OrderByDescending(s => s.TotalScore)
            .ThenBy(s => s.Symbol)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<ReversalCandidateSnapshot>> GetHistoryAsync(
        string symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await db.ReversalCandidateSnapshots.AsNoTracking()
            .Where(s => s.Symbol == symbol && s.TradingDate >= from && s.TradingDate <= to)
            .OrderBy(s => s.TradingDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<ReversalCandidateSnapshot>> GetActionableInRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await db.ReversalCandidateSnapshots.AsNoTracking()
            .Where(s => s.IsActionable && s.EntryReference != null
                        && s.TradingDate >= from && s.TradingDate <= to)
            .OrderBy(s => s.TradingDate)
            .ThenByDescending(s => s.TotalScore)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public Task<int> CountSameSetupPriorAsync(
        string symbol, Guid setupId, string strategyVersion, DateOnly beforeDate,
        CancellationToken cancellationToken = default)
        => db.ReversalCandidateSnapshots.AsNoTracking()
            .CountAsync(
                s => s.Symbol == symbol
                     && s.SetupId == setupId
                     && s.StrategyVersion == strategyVersion
                     && s.TradingDate < beforeDate,
                cancellationToken);

    private static ReversalCandidateSnapshot ToDomain(ReversalCandidateSnapshotEntity e)
    {
        var reasons = string.IsNullOrWhiteSpace(e.ReasonsJson)
            ? []
            : JsonSerializer.Deserialize<List<ReversalBounceReason>>(e.ReasonsJson, EntityMapper.JsonOptions) ?? [];

        ReversalBounceTradePlan? plan = null;
        if (e.EntryReference is not null)
        {
            var warnings = string.IsNullOrWhiteSpace(e.RiskWarningsJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(e.RiskWarningsJson, EntityMapper.JsonOptions) ?? [];
            plan = new ReversalBounceTradePlan(
                EntryReference: e.EntryReference.Value,
                MaxEntryPrice: e.MaxEntryPrice ?? e.EntryReference.Value,
                InvalidationPrice: e.InvalidationPrice ?? 0m,
                FirstTarget: e.FirstTarget ?? 0m,
                RewardToRisk: e.RewardToRisk ?? 0m,
                TimeStopSessions: e.TimeStopSessions ?? 0,
                PositionFactor: e.PositionFactor ?? 0m,
                RiskWarnings: warnings);
        }

        return new ReversalCandidateSnapshot(
            TradingDate: e.TradingDate,
            Symbol: e.Symbol,
            Stage: Enum.TryParse<ReversalBounceStage>(e.Stage, out var stage) ? stage : ReversalBounceStage.None,
            SetupId: e.SetupId,
            CapitulationDate: e.CapitulationDate,
            CapitulationLow: e.CapitulationLow,
            CapitulationClose: e.CapitulationClose,
            RecoveryAttemptCount: e.RecoveryAttemptCount,
            ComponentScores: new ReversalBounceComponentScores(
                e.ScoreCapitulation, e.ScoreStabilization, e.ScoreDemand,
                e.ScoreRelativeStrength, e.ScoreLiquidity, e.ScoreRiskPenalty),
            TotalScore: e.TotalScore,
            MarketRegime: Enum.TryParse<MarketRegime>(e.MarketRegime, out var regime) ? regime : MarketRegime.Normal,
            IsActionable: e.IsActionable,
            TradePlan: plan,
            StrategyVersion: e.StrategyVersion,
            AlgorithmParametersHash: e.AlgorithmParametersHash,
            SchemaVersion: e.SchemaVersion,
            RunBatchId: e.RunBatchId,
            Reasons: reasons,
            CreatedAtUtc: e.CreatedAtUtc);
    }
}
