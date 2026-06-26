using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/sectors")]
[Produces("application/json")]
[Tags("Sectors")]
public sealed class SectorsController(IMarketService market, ISectorCatalogService sectors) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SectorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SectorDto>>> GetAll(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await market.GetSectorsAsync(query, cancellationToken));

    [HttpGet("catalog")]
    [ProducesResponseType(typeof(IReadOnlyList<SectorCatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SectorCatalogItemDto>>> GetCatalog(
        CancellationToken cancellationToken)
    {
        var names = await sectors.GetCatalogAsync(cancellationToken);
        return Ok(names.Select(n => new SectorCatalogItemDto(n)).ToList());
    }
}
