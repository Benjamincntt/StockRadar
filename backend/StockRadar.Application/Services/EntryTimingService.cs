using StockRadar.Application.Abstractions;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>Rebuild thống kê timing Top-only vs Master confirm (dùng đo hiệu quả, không gắn stock detail).</summary>
public sealed class EntryTimingService(
    ISetupTrackRepository tracks,
    IEntryTimingRepository entryTiming)
{
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        var rows = await tracks.GetMeasuredOpportunitiesForEntryTimingAsync(cancellationToken);
        if (rows.Count == 0)
            return;

        var stats = EntryTimingAnalyzer.Aggregate(
            rows.Select(r => (r.HadMasterConfirm == true, r.OutcomeBucket)));
        var hint = EntryTimingAnalyzer.BuildHint(stats);
        await entryTiming.SaveAsync(
            new EntryTimingStateRecord(
                stats.TopOnlyMeasured,
                stats.TopOnlyGood,
                stats.ConfirmMeasured,
                stats.ConfirmGood,
                hint?.PreferMasterConfirm ?? false,
                DateTime.UtcNow),
            cancellationToken);
    }
}
