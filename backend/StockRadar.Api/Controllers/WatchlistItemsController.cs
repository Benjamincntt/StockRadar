using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/watchlist-items")]
[Produces("application/json")]
[Tags("Watchlist")]
public sealed class WatchlistItemsController(IWatchlistService watchlist) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WatchlistItemDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await watchlist.GetItemsAsync(cancellationToken));

    [HttpPut("{symbol}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert(string symbol, CancellationToken cancellationToken)
    {
        var created = await watchlist.AddAsync(symbol, cancellationToken);
        if (created)
            return Created($"/api/v1/watchlist-items/{symbol.ToUpperInvariant()}", null);

        return NoContent();
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWatchlistItemRequest request,
        CancellationToken cancellationToken)
    {
        var created = await watchlist.AddAsync(request.Symbol, cancellationToken);
        var symbol = request.Symbol.ToUpperInvariant();
        if (created)
            return Created($"/api/v1/watchlist-items/{symbol}", null);

        return NoContent();
    }

    [HttpDelete("{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string symbol, CancellationToken cancellationToken)
    {
        var removed = await watchlist.RemoveAsync(symbol, cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
