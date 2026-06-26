using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class SmartMoneyEvaluationService(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISmartMoneyOpportunitySelector selector,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<PriceRunupFilterOptions> runupFilter)
{
    public async Task<(SmartMoneyMarketContext Context, SmartMoneyEvaluation Eval)?> EvaluateAsync(
        Stock stock,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(cancellationToken);
        return (context, selector.Evaluate(stock, context));
    }

    public async Task<SmartMoneyMarketContext> BuildContextAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        return selector.BuildContext(
            all,
            index,
            runupFilter.Value.ToSettings(),
            smartMoneyOptions.Value.ToSettings());
    }

    public SmartMoneyEvaluation EvaluateStock(Stock stock, SmartMoneyMarketContext context) =>
        selector.Evaluate(stock, context);
}
