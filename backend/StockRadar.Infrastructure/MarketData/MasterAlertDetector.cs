using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

internal sealed record MasterAlertSignal(
    string Kind,
    string Title,
    string Message,
    bool IsBuy,
    decimal EntryPrice,
    decimal ChangePercent,
    long SessionVolume,
    decimal PeakGainPercent);

internal sealed class MasterAlertDetector
{
    public IReadOnlyList<MasterAlertSignal> Detect(
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state,
        IReadOnlyList<OrderFlowEvent> flowEvents,
        MasterAlertOptions options)
    {
        if (!options.Enabled)
            return [];

        var results = new List<MasterAlertSignal>();
        state.UpdateHigh(row.High);

        if (!state.BuyPoint1Fired
            && row.ChangePercent >= options.BuyPoint1MinChangePercent
            && row.SessionVolume >= options.MinSessionVolume)
        {
            state.BuyPoint1Fired = true;
            state.BuyPoint1Price = row.Close;
            state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
            results.Add(Build(
                MasterAlertKinds.BuyPoint1,
                row,
                isBuy: true,
                state.PeakGainPercent(),
                $"Rải ngân lần 1 — phiên +{row.ChangePercent:0.#}%, KL {row.SessionVolume:N0}",
                $"Giá {row.Close:N1} · ngưỡng +{options.BuyPoint1MinChangePercent:0.#}% & KL ≥{options.MinSessionVolume:N0}"));
        }

        if (state.BuyPoint1Fired
            && !state.BuyPoint2Fired
            && row.ChangePercent >= options.BuyPoint2MinChangePercent)
        {
            state.BuyPoint2Fired = true;
            results.Add(Build(
                MasterAlertKinds.BuyPoint2,
                row,
                isBuy: true,
                state.PeakGainPercent(),
                $"Rải ngân lần 2 — phiên +{row.ChangePercent:0.#}%",
                $"Giá {row.Close:N1} · cùng phiên đạt +{options.BuyPoint2MinChangePercent:0.#}%"));
        }

        var peakGain = state.PeakGainPercent();
        var hasLargeSell = flowEvents.Any(e =>
            e.Source is OrderFlowSource.LargeAsk or OrderFlowSource.ForeignSell);

        if (state.BuyPoint1Fired
            && !state.CutLoss1Fired
            && peakGain >= options.CutLoss1MinPeakGainPercent
            && hasLargeSell)
        {
            state.CutLoss1Fired = true;
            results.Add(Build(
                MasterAlertKinds.CutLoss1,
                row,
                isBuy: false,
                peakGain,
                $"Cắt lỗ điểm 1 — đỉnh +{peakGain:0.#}% từ mua, lệnh bán lớn",
                $"Giá {row.Close:N1} · đỉnh phiên {state.SessionHighSinceBuy1:N1} từ mua {state.BuyPoint1Price:N1}"));
        }

        if (state.BuyPoint1Fired
            && !state.CutAllFired
            && peakGain >= options.CutAllMinPeakGainPercent
            && hasLargeSell)
        {
            state.CutAllFired = true;
            results.Add(Build(
                MasterAlertKinds.CutAll,
                row,
                isBuy: false,
                peakGain,
                $"Cắt hết — đỉnh +{peakGain:0.#}% từ mua, áp lực bán mạnh",
                $"Giá {row.Close:N1} · thoát toàn bộ vị thế"));
        }

        return results;
    }

    private static MasterAlertSignal Build(
        string kind,
        KbsPriceBoardClient.KbsBoardRow row,
        bool isBuy,
        decimal peakGain,
        string headline,
        string detail) =>
        new(
            kind,
            $"{row.Symbol} — {MasterAlertKinds.Label(kind)}",
            $"{headline}\n{detail}",
            isBuy,
            row.Close,
            row.ChangePercent,
            row.SessionVolume,
            peakGain);
}
