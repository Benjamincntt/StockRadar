using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/performance")]
public sealed class PerformanceController(
    IOpportunityPerformanceQueryService performance) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(OpportunityPerformanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityPerformanceSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await performance.GetSummaryAsync(cancellationToken));
}
