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
    ITradeEventStore tradeEvents,
    ISessionFlowQuery sessionFlow) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MarketOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketOverviewDto>> Get(CancellationToken cancellationToken) =>
        Ok(await market.GetOverviewAsync(cancellationToken));

    [HttpGet("vnindex/chart")]
    [ProducesResponseType(typeof(VnIndexChartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VnIndexChartDto>> GetVnIndexChart(
        [FromQuery] int sessions = 90,
        CancellationToken cancellationToken = default) =>
        Ok(await market.GetVnIndexChartAsync(sessions, cancellationToken));

    [HttpGet("intraday-monitor")]
    [ProducesResponseType(typeof(IntradayMonitorStatusDto), StatusCodes.Status200OK)]
    public ActionResult<IntradayMonitorStatusDto> GetIntradayMonitorStatus() =>
        Ok(monitorStatus.GetStatus());

    [HttpGet("trades")]
    [ProducesResponseType(typeof(IReadOnlyList<TradeEventDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TradeEventDto>> GetTrades(
        [FromQuery] int limit = 50,
        [FromQuery] string? label = null) =>
        Ok(tradeEvents.GetRecent(Math.Clamp(limit, 1, 200), label));

    [HttpGet("flow/{symbol}")]
    [ProducesResponseType(typeof(SessionFlowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<SessionFlowDto> GetSymbolFlow(string symbol)
    {
        var flow = sessionFlow.GetSymbolFlow(symbol);
        return flow is null ? NotFound() : Ok(flow);
    }

    [HttpGet("flow/leaders")]
    [ProducesResponseType(typeof(IReadOnlyList<FlowLeaderDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<FlowLeaderDto>> GetFlowLeaders([FromQuery] int limit = 20) =>
        Ok(sessionFlow.GetLeaders(Math.Clamp(limit, 1, 50)));

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
