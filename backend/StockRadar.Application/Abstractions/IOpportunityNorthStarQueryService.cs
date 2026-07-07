using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IOpportunityNorthStarQueryService
{
    Task<OpportunityNorthStarReportDto> GetReportAsync(
        int days = 90,
        CancellationToken cancellationToken = default);
}
