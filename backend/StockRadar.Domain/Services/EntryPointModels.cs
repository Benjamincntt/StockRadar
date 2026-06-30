using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public interface IEntryPointEvaluator
{
    EntryPointEvaluation Evaluate(Entities.Stock stock, SmartMoneyMarketContext context);
}

public sealed record EntryPointCheck(string Id, string Label, bool Passed, string Detail);

public sealed record EntryPointEvaluation(
    EntryPointStatus Status,
    EntryPointType Type,
    int Confidence,
    decimal EntryPrice,
    decimal StopLoss,
    decimal TriggerPrice,
    decimal TargetPrice,
    decimal BaseLow,
    decimal BaseHigh,
    decimal GainFromBasePercent,
    decimal RiskRewardRatio,
    bool IsActionable,
    string Headline,
    string Action,
    IReadOnlyList<EntryPointCheck> Checklist);
