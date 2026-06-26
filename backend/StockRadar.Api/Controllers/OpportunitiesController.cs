using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/opportunities")]
[Produces("application/json")]
[Tags("Opportunities")]
public sealed class OpportunitiesController(IMarketService market) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OpportunitiesListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunitiesListDto>> GetAll(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await market.GetOpportunitiesAsync(query, cancellationToken));

    /// <summary>Chạy lại phân tích SmartMoney (Job phân tích) — tạo watchlist mới nhất.</summary>
    [HttpPost("run-analysis")]
    [ProducesResponseType(typeof(DailyAnalysisResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyAnalysisResultDto>> RunAnalysis(
        CancellationToken cancellationToken) =>
        Ok(await market.RunOpportunityAnalysisAsync(cancellationToken));
}
