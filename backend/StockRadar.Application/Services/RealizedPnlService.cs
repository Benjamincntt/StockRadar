using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>
/// Đo lợi nhuận thực (realized P&amp;L) cho các vị thế Master Alert đã đóng — dùng giá tại tín hiệu
/// Bán 1 nửa/Bán hết (<see cref="PositionSellLegRecord"/>), trừ phí + thuế (<see cref="RealizedPnlMath"/>).
/// Song song với T+2.5 (<see cref="OpportunityPerformanceRunner"/> không đụng luồng đó) — xem plan §5.
/// </summary>
public sealed class RealizedPnlService(
    IMasterAlertPositionRepository positions,
    IOptions<RealizedPnlOptions> options,
    ILogger<RealizedPnlService> logger)
{
    public async Task<int> MeasureClosedPositionsAsync(CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
            return 0;

        var fees = new FeeProfile(cfg.BuyFeePercent, cfg.SellFeePercent, cfg.SellTaxPercent);
        var feeKey = fees.Key(cfg.WinThresholdPercent);

        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, cfg.MeasureLookbackSessions);

        var pending = await positions.GetClosedPendingRealizedAsync(fromDate, feeKey, ct);
        if (pending.Count == 0)
            return 0;

        var legsByPosition = await LoadLegsByPositionAsync(pending.Select(p => p.Id).ToList(), ct);

        var measured = 0;
        foreach (var position in pending)
        {
            legsByPosition.TryGetValue(position.Id, out var legs);
            await MeasureOneAsync(position, legs ?? [], fees, feeKey, cfg.WinThresholdPercent, ct);
            measured++;
        }

        if (measured > 0)
            logger.LogInformation("Đo realized P&L: {Count} vị thế đã đóng.", measured);

        return measured;
    }

    private async Task MeasureOneAsync(
        MasterAlertPositionRecord position,
        IReadOnlyList<PositionSellLegRecord> rawLegs,
        FeeProfile fees,
        string feeKey,
        decimal winThresholdPercent,
        CancellationToken ct)
    {
        if (position.EntryPrice <= 0 || rawLegs.Count == 0)
        {
            // Không dựng được leg nào (hoặc giá vào lệnh không hợp lệ) — đánh dấu MissingSellPrice
            // để không quét lại vô hạn, các cột % để null.
            await positions.SaveRealizedAsync(
                position.Id,
                RealizedStatusNames.MissingSellPrice,
                feeKey,
                null,
                null,
                null,
                null,
                null,
                ct);
            return;
        }

        var legs = rawLegs
            .Select(l => new SellLeg(l.SellDate, l.SellPrice, l.SoldSize))
            .ToList();

        var result = RealizedPnlMath.Compute(position.EntryPrice, legs, fees);
        if (result is null)
        {
            await positions.SaveRealizedAsync(
                position.Id,
                RealizedStatusNames.MissingSellPrice,
                feeKey,
                null,
                null,
                null,
                null,
                null,
                ct);
            return;
        }

        if (position.MaxPositionSize > 0 && Math.Abs(result.TotalSoldSize - position.MaxPositionSize) > 0.01m)
        {
            logger.LogWarning(
                "Realized P&L {Symbol}: Σ SoldSize {Total} lệch MaxPositionSize {Max} (vị thế {Id}) — vẫn chia theo Σ thực.",
                position.Symbol,
                result.TotalSoldSize,
                position.MaxPositionSize,
                position.Id);
        }

        // Approximate nếu có bất kỳ leg nào không phải giá bắn noti VIP thật (backfill T+2.5/OHLCV close).
        var status = rawLegs.Any(l => !string.Equals(l.PriceSource, "Fire", StringComparison.Ordinal))
            ? RealizedStatusNames.Approximate
            : RealizedStatusNames.Measured;

        var bucket = RealizedPnlMath.Classify(result.ReturnOnDeployedPercent, winThresholdPercent);
        var holdingSessions = position.ClosedDate is null
            ? (int?)null
            : TradingSessionMath.TradingSessionsBetween(position.EntryDate, position.ClosedDate.Value);

        await positions.SaveRealizedAsync(
            position.Id,
            status,
            feeKey,
            result.WeightedReturnPercent,
            result.ReturnOnDeployedPercent,
            result.GrossReturnOnDeployedPercent,
            bucket,
            holdingSessions,
            ct);
    }

    private async Task<IReadOnlyDictionary<Guid, List<PositionSellLegRecord>>> LoadLegsByPositionAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken ct)
    {
        var legs = await positions.GetSellLegsAsync(positionIds, ct);
        var map = new Dictionary<Guid, List<PositionSellLegRecord>>();
        foreach (var leg in legs)
        {
            if (!map.TryGetValue(leg.PositionId, out var list))
            {
                list = [];
                map[leg.PositionId] = list;
            }

            list.Add(leg);
        }

        return map;
    }
}
