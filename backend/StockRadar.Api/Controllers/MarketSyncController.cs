using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/market")]
[Produces("application/json")]
[Tags("Market Sync")]
public sealed class MarketSyncController(
    IMarketSyncService sync,
    IOptions<MarketDataOptions> options) : ControllerBase
{
    [HttpPost("sync")]
    [ProducesResponseType(typeof(MarketSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MarketSyncResultDto>> Sync(
        [FromBody] MarketSyncRequest request,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        var result = await sync.ApplyAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sync/symbols")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSymbols(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        return Ok(await sync.GetTrackedSymbolsAsync(cancellationToken));
    }

    private bool IsAuthorized(string? syncKey) =>
        !string.IsNullOrWhiteSpace(options.Value.SyncApiKey)
        && syncKey == options.Value.SyncApiKey;
}
