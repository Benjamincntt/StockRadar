using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/trade-journal")]
public sealed class TradeJournalController(ITradeJournalService journal) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TradeJournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TradeJournalEntryDto>>> GetRecent(
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await journal.GetRecentAsync(limit, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(TradeJournalEntryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TradeJournalEntryDto>> Create(
        [FromBody] CreateTradeJournalRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await journal.AddAsync(request, cancellationToken);
        return Created($"/api/v1/trade-journal/{entry.Id}", entry);
    }

    [HttpGet("calibration")]
    [ProducesResponseType(typeof(PersonalCalibrationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<PersonalCalibrationDto>> GetCalibration(
        CancellationToken cancellationToken = default)
    {
        var cal = await journal.GetPersonalCalibrationAsync(cancellationToken);
        return cal is null ? NoContent() : Ok(cal);
    }
}
