using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/criteria")]
public sealed class CriteriaController(ICriterionScoringService criteria) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await criteria.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    /// <summary>So sánh các bộ trọng số reliability trên dữ liệu quá khứ.</summary>
    [HttpGet("reliability-backtest")]
    public async Task<IActionResult> BacktestReliability(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await criteria.BacktestReliabilityWeightsAsync(days, cancellationToken);
        return Ok(result);
    }
}
