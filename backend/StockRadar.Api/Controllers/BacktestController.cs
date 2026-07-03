using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/backtest")]
public sealed class BacktestController(IBacktestService backtest) : ControllerBase
{
    /// <summary>Replay SmartMoney trên lịch sử OHLCV — win rate, drawdown đa mã.</summary>
    [HttpGet("smartmoney")]
    [ProducesResponseType(typeof(SmartMoneyBacktestResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SmartMoneyBacktestResultDto>> RunSmartMoney(
        [FromQuery] int days = 90,
        [FromQuery] int maxPicksPerDay = 10,
        [FromQuery] int holdSessions = 5,
        [FromQuery] bool relaxedFallback = true,
        [FromQuery] string mode = "strict-then-relaxed",
        [FromQuery] int? minScore = null,
        CancellationToken cancellationToken = default)
    {
        var parsedMode = mode.Trim().ToLowerInvariant() switch
        {
            "strict" => SmartMoneyBacktestMode.Strict,
            "relaxed" => SmartMoneyBacktestMode.Relaxed,
            _ => SmartMoneyBacktestMode.StrictThenRelaxed
        };

        return Ok(await backtest.RunSmartMoneyAsync(
            new SmartMoneyBacktestRequestDto(
                days, maxPicksPerDay, holdSessions, relaxedFallback, minScore, parsedMode),
            cancellationToken));
    }
}
