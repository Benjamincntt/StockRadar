using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/early-recovery")]
[Produces("application/json")]
[Tags("Early Recovery")]
public sealed class EarlyRecoveryController(IMarketService market) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(EarlyRecoveryListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EarlyRecoveryListDto>> GetAll(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await market.GetEarlyRecoveryAsync(query, cancellationToken));
}
