using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

/// <summary>Multi-watchlist: danh sách mặc định + danh sách ngành (tự động) + danh sách tùy chỉnh.</summary>
[ApiController]
[Route("api/v1/watchlists")]
[Produces("application/json")]
[Tags("Watchlists")]
public sealed class WatchlistsController(IWatchlistService watchlist) : ControllerBase
{
    /// <summary>Tất cả danh sách của user hiện tại — lazy seed mặc định + ngành lần đầu gọi.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WatchlistDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await watchlist.GetWatchlistsAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(WatchlistDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WatchlistDto>> Create(
        [FromBody] CreateWatchlistRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await watchlist.CreateWatchlistAsync(request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(WatchlistDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await watchlist.GetWatchlistAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        int id,
        [FromBody] RenameWatchlistRequest request,
        CancellationToken cancellationToken) =>
        await watchlist.RenameWatchlistAsync(id, request.Name, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await watchlist.DeleteWatchlistAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();

    /// <summary>Items của một danh sách (enrich giá trị + điểm). Danh sách ngành: items động theo ngành.</summary>
    [HttpGet("{id:int}/items")]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WatchlistItemDto>>> GetItems(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await watchlist.GetWatchlistItemsAsync(id, cancellationToken));

    [HttpPut("{id:int}/items/{symbol}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertItem(int id, string symbol, CancellationToken cancellationToken)
    {
        var created = await watchlist.AddItemAsync(id, symbol, cancellationToken);
        return created
            ? Created($"/api/v1/watchlists/{id}/items/{symbol.ToUpperInvariant()}", null)
            : NoContent();
    }

    [HttpDelete("{id:int}/items/{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(int id, string symbol, CancellationToken cancellationToken) =>
        await watchlist.RemoveItemAsync(id, symbol, cancellationToken)
            ? NoContent()
            : NotFound();
}
