using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
[Produces("application/json")]
[Tags("Alerts")]
public sealed class AlertsController(IAlertService alerts) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AlertDto>>> GetAll(
        [FromQuery] AlertQuery query,
        CancellationToken cancellationToken) =>
        Ok(await alerts.GetAlertsAsync(query, cancellationToken));
}
