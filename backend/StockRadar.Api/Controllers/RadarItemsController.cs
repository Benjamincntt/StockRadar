using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/radar-items")]
[Produces("application/json")]
[Tags("Radar")]
public sealed class RadarItemsController(IRadarService radar) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType(typeof(RadarLiveSnapshotDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RadarLiveSnapshotDto>> GetLive(
        [FromQuery] RadarLiveQuery query,
        CancellationToken cancellationToken) =>
        Ok(await radar.GetLiveRadarAsync(query, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RadarItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RadarItemDto>>> GetAll(
        [FromQuery] RadarQuery query,
        CancellationToken cancellationToken) =>
        Ok(await radar.GetRadarAsync(query, cancellationToken));
}
