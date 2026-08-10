using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfVipAlertFireRepository(ApplicationDbContext db) : IVipAlertFireRepository
{
    public async Task AddAsync(VipAlertFireRecord fire, CancellationToken cancellationToken = default)
    {
        db.VipAlertFires.Add(ToEntity(fire));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TouchSessionRangeAsync(
        string symbol,
        DateOnly sessionDate,
        decimal high,
        decimal low,
        CancellationToken cancellationToken = default)
    {
        if (high <= 0 && low <= 0)
            return;

        var rows = await db.VipAlertFires
            .Where(x => x.SessionDate == sessionDate
                        && x.Symbol == symbol
                        && !x.IntradayMeasured)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return;

        foreach (var row in rows)
        {
            if (high > 0)
                row.SessionHighSinceFire = Math.Max(row.SessionHighSinceFire ?? row.FirePrice, high);
            if (low > 0)
                row.SessionLowSinceFire = Math.Min(row.SessionLowSinceFire ?? row.FirePrice, low);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VipAlertFireRecord>> GetPendingIntradayAsync(
        DateOnly sessionDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.VipAlertFires
            .AsNoTracking()
            .Where(x => x.SessionDate == sessionDate && !x.IntradayMeasured)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    public async Task MarkIntradayMeasuredAsync(
        Guid id,
        decimal closePrice,
        decimal? sessionHigh,
        decimal? sessionLow,
        CancellationToken cancellationToken = default)
    {
        var row = await db.VipAlertFires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null || row.IntradayMeasured || row.FirePrice <= 0)
            return;

        var ret = Math.Round((closePrice - row.FirePrice) / row.FirePrice * 100m, 2);
        var high = sessionHigh ?? row.SessionHighSinceFire ?? closePrice;
        var low = sessionLow ?? row.SessionLowSinceFire ?? closePrice;
        var mfe = Math.Round((high - row.FirePrice) / row.FirePrice * 100m, 2);
        var mae = Math.Round((low - row.FirePrice) / row.FirePrice * 100m, 2);

        row.IntradayReturnPercent = ret;
        row.IntradayMfePercent = mfe;
        row.IntradayMaePercent = mae;
        row.SessionHighSinceFire = high;
        row.SessionLowSinceFire = low;
        row.IntradayMeasured = true;
        row.IntradayMeasuredAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VipAlertFireRecord>> GetSinceAsync(
        DateOnly fromSessionDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.VipAlertFires
            .AsNoTracking()
            .Where(x => x.SessionDate >= fromSessionDate)
            .OrderByDescending(x => x.FiredAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    private static VipAlertFireEntity ToEntity(VipAlertFireRecord r) => new()
    {
        Id = r.Id,
        Symbol = r.Symbol,
        SessionDate = r.SessionDate,
        FiredAtUtc = r.FiredAtUtc,
        Signal = r.Signal,
        Branch = r.Branch,
        FirePrice = r.FirePrice,
        OpenPrice = r.OpenPrice,
        GainFromOpenPercent = r.GainFromOpenPercent,
        PacedVolumeRatio = r.PacedVolumeRatio,
        MlProbAtFire = r.MlProbAtFire,
        MlModelActive = r.MlModelActive,
        BuyScore = r.BuyScore,
        PredictedHitPercent = r.PredictedHitPercent,
        MarketPhase = r.MarketPhase,
        Rs5dPercent = r.Rs5dPercent,
        AtrPercent = r.AtrPercent,
        DistMa20Percent = r.DistMa20Percent,
        Ma10 = r.Ma10,
        Ma20 = r.Ma20,
        Ma50 = r.Ma50,
        UptrendLong = r.UptrendLong,
        ForeignNet = r.ForeignNet,
        PropNet = r.PropNet,
        SessionPressure = r.SessionPressure,
        VsaLabel = r.VsaLabel,
        FeaturesComplete = r.FeaturesComplete,
        IntradayMeasured = r.IntradayMeasured,
        IntradayReturnPercent = r.IntradayReturnPercent,
        IntradayMfePercent = r.IntradayMfePercent,
        IntradayMaePercent = r.IntradayMaePercent,
        SessionHighSinceFire = r.SessionHighSinceFire,
        SessionLowSinceFire = r.SessionLowSinceFire,
        LlmDecision = r.LlmDecision,
        LlmReason = r.LlmReason,
        LlmLatencyMs = r.LlmLatencyMs,
        LlmModel = r.LlmModel,
        LlmShadowMode = r.LlmShadowMode,
        SellContextJson = r.SellContextJson,
    };

    private static VipAlertFireRecord ToRecord(VipAlertFireEntity e) => new(
        e.Id,
        e.Symbol,
        e.SessionDate,
        e.FiredAtUtc,
        e.Signal,
        e.Branch,
        e.FirePrice,
        e.OpenPrice,
        e.GainFromOpenPercent,
        e.PacedVolumeRatio,
        e.MlProbAtFire,
        e.MlModelActive,
        e.BuyScore,
        e.PredictedHitPercent,
        e.MarketPhase,
        e.Rs5dPercent,
        e.AtrPercent,
        e.DistMa20Percent,
        e.Ma10,
        e.Ma20,
        e.Ma50,
        e.UptrendLong,
        e.ForeignNet,
        e.PropNet,
        e.SessionPressure,
        e.VsaLabel,
        e.FeaturesComplete,
        e.IntradayMeasured,
        e.IntradayReturnPercent,
        e.IntradayMfePercent,
        e.IntradayMaePercent,
        e.SessionHighSinceFire,
        e.SessionLowSinceFire,
        e.LlmDecision,
        e.LlmReason,
        e.LlmLatencyMs,
        e.LlmModel,
        e.LlmShadowMode,
        e.SellContextJson);
}
