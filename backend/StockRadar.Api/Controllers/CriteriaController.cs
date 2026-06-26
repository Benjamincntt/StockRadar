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
}
