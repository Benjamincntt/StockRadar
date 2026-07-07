using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/performance")]
public sealed class PerformanceController(
    IOpportunityPerformanceQueryService performance,
    IOpportunityNorthStarQueryService northStar) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(OpportunityPerformanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityPerformanceSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await performance.GetSummaryAsync(cancellationToken));

    /// <summary>North Star — hit T+2.5 theo rank Top 3/5/10 và TradeState (Phase 1 baseline).</summary>
    [HttpGet("north-star")]
    [ProducesResponseType(typeof(OpportunityNorthStarReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityNorthStarReportDto>> GetNorthStar(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default) =>
        Ok(await northStar.GetReportAsync(days, cancellationToken));
}
