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
        string kind = "buy",
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task<AlertHistoryTrendsResponseDto> GetAlertHistoryTrendsAsync(
        string period = "week",
        string kind = "buy",
        int limit = 12,
        DateOnly? selectedPeriodStart = null,
        CancellationToken cancellationToken = default);
}
