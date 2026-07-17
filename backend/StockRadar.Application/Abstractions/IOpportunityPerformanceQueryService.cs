using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IOpportunityPerformanceQueryService
{
    Task<OpportunityPerformanceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<AlertHistoryResponseDto> GetAlertHistoryAsync(
        int limit = 50,
        int skip = 0,
        string? status = null,
        string? alertType = null,
        CancellationToken cancellationToken = default);
}
