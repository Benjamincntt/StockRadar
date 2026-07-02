using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/market")]
[Produces("application/json")]
[Tags("Market")]
public sealed class MarketController(
    IMarketService market,
    IIntradayMonitorStatusQuery monitorStatus,
    IStockLookupService stockLookup,
    ITradePrintStore tradePrints) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MarketOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketOverviewDto>> Get(CancellationToken cancellationToken) =>
        Ok(await market.GetOverviewAsync(cancellationToken));

    [HttpGet("intraday-monitor")]
    [ProducesResponseType(typeof(IntradayMonitorStatusDto), StatusCodes.Status200OK)]
    public ActionResult<IntradayMonitorStatusDto> GetIntradayMonitorStatus() =>
        Ok(monitorStatus.GetStatus());

    [HttpGet("trades")]
    [ProducesResponseType(typeof(IReadOnlyList<TradePrintDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TradePrintDto>> GetTrades([FromQuery] int limit = 50) =>
        Ok(tradePrints.GetRecent(Math.Clamp(limit, 1, 200)));

    [HttpGet("quotes")]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteTickDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuoteTickDto>>> GetQuotes(CancellationToken cancellationToken) =>
        Ok(await market.GetQuoteSnapshotAsync(cancellationToken));

    [HttpGet("sparklines")]
    [ProducesResponseType(typeof(IReadOnlyList<SparklineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SparklineDto>>> GetSparklines(
        [FromQuery] string symbols,
        CancellationToken cancellationToken)
    {
        var list = string.IsNullOrWhiteSpace(symbols)
            ? []
            : symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Ok(await market.GetSparklinesAsync(list, cancellationToken));
    }

    [HttpGet("stock-search")]
    [ProducesResponseType(typeof(IReadOnlyList<StockSearchHitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockSearchHitDto>>> SearchStocks(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await stockLookup.SearchAsync(q, limit, cancellationToken));
}
