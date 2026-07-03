using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Cảnh báo breakout hộp Darvas trên toàn universe sau Job 2.</summary>
internal sealed class DarvasBreakoutAlertPublisher(
    ISignalAnalyzer signalAnalyzer,
    IAlertRepository alerts,
    IMarketRealtimePublisher publisher,
    IOptions<PriceRunupFilterOptions> runupFilter)
{
    public const string SourceTag = "Phá vỡ hộp tích lũy phẳng";

    public async Task<int> PublishAsync(
        IReadOnlyList<Stock> stocks,
        decimal indexChangePercent,
        CancellationToken cancellationToken)
    {
        var filter = runupFilter.Value.ToSettings();
        var sent = 0;

        foreach (var stock in stocks)
        {
            if (stock.History.Count < filter.ConsolidationMinSessions + 1)
                continue;

            var current = signalAnalyzer.DetectSignals(stock, indexChangePercent, filter);
            if (!current.Contains(SignalType.DarvasBreakout))
                continue;

            var trimmed = stock with
            {
                History = stock.History.Take(stock.History.Count - 1).ToList()
            };
            var previous = signalAnalyzer.DetectSignals(trimmed, indexChangePercent, filter);
            if (previous.Contains(SignalType.DarvasBreakout))
                continue;

            var breakout = signalAnalyzer.EvaluateDarvasBreakout(stock.History, filter);
            if (!breakout.IsValidBreakout)
                continue;

            var volumeRatio = signalAnalyzer.GetVolumeRatio(stock.History);
            var relativeStrength = signalAnalyzer.GetRelativeStrength(stock, indexChangePercent);
            var title = $"{stock.Symbol} — Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền";
            var message =
                $"Phá đỉnh hộp {breakout.BoxMaxClose:N2} (+{breakout.PriceGainPercent:0.#}%), "
                + $"KL ×{breakout.VolumeMultiplier:0.0} so TB nền. "
                + $"Hộp {breakout.RefBoxPeriod}. "
                + $"Cắt lỗ gợi ý {breakout.SuggestedStopLoss:N2}.";

            var alert = new Alert(
                Guid.NewGuid(),
                stock.Symbol,
                SignalType.DarvasBreakout,
                title,
                message,
                DateTime.UtcNow,
                AlertCategory.Buy,
                volumeRatio,
                relativeStrength,
                SourceTag);

            await alerts.AddAsync(alert, cancellationToken);
            await publisher.PublishAlertAsync(DtoMapper.ToDto(alert), cancellationToken);
            sent++;
        }

        return sent;
    }
}
