using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public sealed class EntryPointEvaluator(IBuyDecisionEngine buyDecision) : IEntryPointEvaluator
{
    public EntryPointEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context) =>
        buyDecision.Evaluate(stock, context).Entry;
}
