using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;

namespace StockRadar.Application.Services;

public sealed class AlertService(
    IAlertRepository alerts,
    IDailyOpportunityRepository opportunities,
    IJobStockRepository stocks,
    IWatchlistRepository watchlist) : IAlertService
{
    public const int MaxAlerts = 20;
    public const string IntradayOrderFlowSource = "Trong phiên";

    public async Task<PagedResult<AlertDto>> GetAlertsAsync(
        AlertQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Normalize(MaxAlerts);
        query.PageSize = Math.Min(query.PageSize, MaxAlerts);

        var opportunitySymbols = await GetOpportunitySymbolsAsync(cancellationToken);
        var watchlistSymbols = await GetWatchlistSymbolsAsync(cancellationToken);

        var allowedSymbols = query.Feed switch
        {
            AlertFeedScope.Universe => await GetUniverseSymbolsAsync(cancellationToken),
            _ => opportunitySymbols.Union(watchlistSymbols).ToHashSet(StringComparer.OrdinalIgnoreCase),
        };

        if (allowedSymbols.Count == 0)
        {
            return new PagedResult<AlertDto>
            {
                Items = [],
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = 0,
            };
        }

        var all = await alerts.GetAllAsync(cancellationToken);
        var results = all
            .Where(a => allowedSymbols.Contains(a.Symbol))
            .Where(a => string.Equals(a.SectorRank, IntradayOrderFlowSource, StringComparison.Ordinal));

        if (query.Category != Domain.Enums.AlertCategory.All)
            results = results.Where(a => a.Category == query.Category);

        if (query.Type is not null)
            results = results.Where(a => a.Type == query.Type);

        var ordered = results
            .OrderByDescending(a =>
                opportunitySymbols.Contains(a.Symbol) && watchlistSymbols.Contains(a.Symbol))
            .ThenByDescending(a => a.CreatedAt)
            .Take(MaxAlerts)
            .Select(a => DtoMapper.ToDto(
                a,
                opportunitySymbols.Contains(a.Symbol),
                watchlistSymbols.Contains(a.Symbol)))
            .ToList();

        return new PagedResult<AlertDto>
        {
            Items = ordered,
            Page = 1,
            PageSize = MaxAlerts,
            TotalCount = ordered.Count,
        };
    }

    private async Task<HashSet<string>> GetUniverseSymbolsAsync(CancellationToken cancellationToken)
    {
        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        return symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> GetWatchlistSymbolsAsync(CancellationToken cancellationToken)
    {
        var symbols = await watchlist.GetSymbolsAsync(cancellationToken);
        return symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> GetOpportunitySymbolsAsync(CancellationToken cancellationToken)
    {
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var opps = await opportunities.GetForDateAsync(targetDate, cancellationToken);

        if (opps.Count == 0)
        {
            var latest = await opportunities.GetLatestForDateAsync(cancellationToken);
            if (latest is not null)
                opps = await opportunities.GetForDateAsync(latest.Value, cancellationToken);
        }

        return opps.Select(o => o.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
