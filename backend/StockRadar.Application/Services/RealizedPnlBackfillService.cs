using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>
/// Backfill realized P&amp;L cho vị thế đã đóng trước khi có <c>PositionSellLegs</c> — xem plan §6.
/// Bước A: gắn <c>PositionId</c> cho SetupTracks cũ. Bước B: dựng leg giá bán theo thứ tự ưu tiên
/// VipAlertFires (giá thật) → T+2.5 (gần đúng). Bước C: gọi <see cref="RealizedPnlService"/> đo.
/// Re-runnable, idempotent (check-then-insert theo unique index), dry-run được.
/// </summary>
public sealed class RealizedPnlBackfillService(
    ISetupTrackRepository setupTracks,
    IMasterAlertPositionRepository positions,
    IVipAlertFireRepository vipFires,
    IJobStockRepository stocks,
    RealizedPnlService realizedPnl) : IRealizedPnlBackfillService
{
    public async Task<RealizedPnlBackfillResultDto> BackfillAsync(
        int days = 365,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var lookback = Math.Clamp(days, 1, 3650);
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, lookback);

        var (tracksLinked, ambiguousTracks) = await LinkTracksAsync(fromDate, today, dryRun, ct);

        var candidates = await positions.GetClosedWithoutLegsAsync(fromDate, ct);
        var stockMap = (await stocks.GetAllAsync(ct))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

        var legsFromFires = 0;
        var legsFromForwardT25 = 0;
        var missingSellPricePositions = 0;

        foreach (var position in candidates)
        {
            if (await TryBuildFromFiresAsync(position, dryRun, ct))
            {
                legsFromFires++;
                continue;
            }

            if (await TryBuildFromForwardT25Async(position, stockMap, dryRun, ct))
            {
                legsFromForwardT25++;
                continue;
            }

            missingSellPricePositions++;
        }

        var measured = dryRun ? 0 : await realizedPnl.MeasureClosedPositionsAsync(ct);
        var approximatePositions = legsFromForwardT25;

        var summary =
            $"Backfill {candidates.Count} vị thế đóng chưa có leg: {legsFromFires} dựng từ VipAlertFires (giá thật), " +
            $"{legsFromForwardT25} gần đúng (T+2.5), {missingSellPricePositions} không dựng được giá bán. " +
            $"Gắn PositionId cho {tracksLinked} track ({ambiguousTracks} mơ hồ — trùng biên vị thế). " +
            (dryRun ? "Dry-run — chưa ghi DB." : $"Đã đo realized {measured} vị thế.");

        return new RealizedPnlBackfillResultDto(
            lookback,
            fromDate,
            candidates.Count,
            tracksLinked,
            ambiguousTracks,
            legsFromFires,
            legsFromForwardT25,
            approximatePositions,
            missingSellPricePositions,
            measured,
            dryRun,
            summary);
    }

    /// <summary>
    /// Bước A: mỗi track Mua điểm 1/2 chưa gắn PositionId được gắn vào vị thế cùng mã có
    /// [EntryDate, ClosedDate ?? hôm nay] chứa EntryDate của track. Nếu khớp ≥ 2 vị thế (trùng biên
    /// ClosedDate == EntryDate vị thế sau) → chọn vị thế có EntryDate gần nhất ≤ track.EntryDate, đếm ambiguous.
    /// </summary>
    private async Task<(int Linked, int Ambiguous)> LinkTracksAsync(
        DateOnly fromDate,
        DateOnly today,
        bool dryRun,
        CancellationToken ct)
    {
        var allPositions = await positions.GetPositionsSinceAsync(fromDate, ct);
        var positionsBySymbol = allPositions
            .GroupBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.EntryDate).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var unlinkedTracks = await setupTracks.GetUnlinkedBuyTracksSinceAsync(fromDate, ct);

        var linked = 0;
        var ambiguous = 0;
        foreach (var track in unlinkedTracks)
        {
            if (!positionsBySymbol.TryGetValue(track.Symbol, out var candidates))
                continue;

            var matches = candidates
                .Where(p => track.EntryDate >= p.EntryDate && track.EntryDate <= (p.ClosedDate ?? today))
                .ToList();
            if (matches.Count == 0)
                continue;

            var chosen = matches.Count == 1
                ? matches[0]
                : matches.OrderByDescending(p => p.EntryDate).First();
            if (matches.Count > 1)
                ambiguous++;

            if (!dryRun)
                await setupTracks.SetPositionIdAsync(track.Id, chosen.Id, ct);
            linked++;
        }

        return (linked, ambiguous);
    }

    /// <summary>Ưu tiên 1: dựng leg từ VipAlertFires (giá tại tín hiệu bán thật) — PriceSource = "Fire".</summary>
    private async Task<bool> TryBuildFromFiresAsync(
        MasterAlertPositionRecord position,
        bool dryRun,
        CancellationToken ct)
    {
        if (position.ClosedDate is null)
            return false;

        var fires = await vipFires.GetSellFiresInRangeAsync(
            position.Symbol, position.EntryDate, position.ClosedDate.Value, ct);
        if (fires.Count == 0)
            return false;

        var half = fires
            .Where(f => f.Signal == MasterAlertKinds.SellPoint1Half)
            .OrderBy(f => f.SessionDate)
            .LastOrDefault();
        var all = fires
            .Where(f => f.Signal == MasterAlertKinds.SellAll)
            .OrderBy(f => f.SessionDate)
            .LastOrDefault();

        if (half is null && all is null)
            return false;

        if (dryRun)
            return true;

        var inserted = false;
        if (half is not null && half.FirePrice > 0)
        {
            var soldSize = position.MaxPositionSize / 2m;
            var ok = await positions.InsertBackfillLegIfMissingAsync(
                position.Id,
                position.Symbol,
                MasterAlertKinds.SellPoint1Half,
                half.SessionDate,
                half.FirePrice,
                soldSize,
                position.MaxPositionSize - soldSize,
                "Fire",
                half.FiredAtUtc,
                ct);
            inserted = inserted || ok;
        }

        if (all is not null && all.FirePrice > 0)
        {
            var soldSize = half is not null ? position.MaxPositionSize - position.MaxPositionSize / 2m : position.MaxPositionSize;
            var ok = await positions.InsertBackfillLegIfMissingAsync(
                position.Id,
                position.Symbol,
                MasterAlertKinds.SellAll,
                all.SessionDate,
                all.FirePrice,
                soldSize,
                0m,
                "Fire",
                all.FiredAtUtc,
                ct);
            inserted = inserted || ok;
        }

        return inserted;
    }

    /// <summary>
    /// Ưu tiên 2: giá T+2.5 từ lịch sử OHLCV — 1 leg BanHet duy nhất, không suy diễn nhịp bán nửa
    /// (không biết ngày bán nửa; bịa giá sai hơn là gộp 1 leg). PriceSource = "ForwardT25".
    /// </summary>
    private async Task<bool> TryBuildFromForwardT25Async(
        MasterAlertPositionRecord position,
        IReadOnlyDictionary<string, StockRadar.Domain.Entities.Stock> stockMap,
        bool dryRun,
        CancellationToken ct)
    {
        if (!stockMap.TryGetValue(position.Symbol, out var stock))
            return false;

        var forward = TradingSessionMath.GetForwardPriceT25(stock.History, position.EntryDate);
        if (forward is null || forward <= 0)
            return false;

        if (dryRun)
            return true;

        var sellDate = TradingSessionMath.AddTradingSessions(position.EntryDate, 3);
        return await positions.InsertBackfillLegIfMissingAsync(
            position.Id,
            position.Symbol,
            MasterAlertKinds.SellAll,
            sellDate,
            forward.Value,
            position.MaxPositionSize,
            0m,
            "ForwardT25",
            DateTime.UtcNow,
            ct);
    }
}
