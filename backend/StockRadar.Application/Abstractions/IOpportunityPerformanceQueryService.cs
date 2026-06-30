using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IOpportunityPerformanceQueryService
{
    Task<OpportunityPerformanceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
