namespace StockRadar.Domain.Services;

public sealed record EntryTimingStats(
    int TopOnlyMeasured,
    int TopOnlyGood,
    int ConfirmMeasured,
    int ConfirmGood)
{
    public decimal TopOnlyRate =>
        TopOnlyMeasured > 0
            ? Math.Round(100m * TopOnlyGood / TopOnlyMeasured, 1)
            : 0;

    public decimal ConfirmRate =>
        ConfirmMeasured > 0
            ? Math.Round(100m * ConfirmGood / ConfirmMeasured, 1)
            : 0;
}

public static class EntryTimingAnalyzer
{
    private const int MinSamples = 8;
    private const decimal MinEdgeGap = 8m;

    public static EntryTimingHint? BuildHint(EntryTimingStats stats)
    {
        if (stats.TopOnlyMeasured < MinSamples && stats.ConfirmMeasured < MinSamples)
            return null;

        var preferConfirm = stats.ConfirmMeasured >= MinSamples
            && stats.TopOnlyMeasured >= MinSamples
            && stats.ConfirmRate >= stats.TopOnlyRate + MinEdgeGap;

        return new EntryTimingHint(
            stats.TopOnlyRate,
            stats.ConfirmRate,
            stats.TopOnlyMeasured,
            stats.ConfirmMeasured,
            preferConfirm);
    }

    public static EntryTimingStats Aggregate(
        IEnumerable<(bool HadMasterConfirm, string? OutcomeBucket)> rows)
    {
        var topOnly = rows.Where(r => !r.HadMasterConfirm).ToList();
        var confirm = rows.Where(r => r.HadMasterConfirm).ToList();
        return new EntryTimingStats(
            topOnly.Count,
            topOnly.Count(r => r.OutcomeBucket == "Good"),
            confirm.Count,
            confirm.Count(r => r.OutcomeBucket == "Good"));
    }
}
