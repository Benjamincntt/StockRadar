using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/signals")]
[Produces("application/json")]
[Tags("Signals")]
public sealed class SignalsController(IMarketService market) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SignalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SignalDto>>> GetAll(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await market.GetSignalsAsync(query, cancellationToken));
}
