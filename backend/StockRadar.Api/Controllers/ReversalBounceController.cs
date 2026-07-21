using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/reversal-bounce")]
[Produces("application/json")]
[Tags("Reversal Bounce")]
public sealed class ReversalBounceController(
    IReversalBounceQueryService reversalBounce,
    IReversalBounceBacktestService backtest,
    IReversalBounceShadowReportService shadow) : ControllerBase
{
    /// <summary>Regime thị trường + độ rộng cho chiến lược counter-trend (Sóng hồi).</summary>
    [HttpGet("market-regime")]
    [ProducesResponseType(typeof(MarketRegimeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketRegimeDto>> GetMarketRegime(CancellationToken cancellationToken) =>
        Ok(await reversalBounce.GetMarketRegimeAsync(cancellationToken));

    /// <summary>Danh sách ứng viên sóng hồi theo phiên (lọc stage / actionable, phân trang).</summary>
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(ReversalBounceListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReversalBounceListDto>> GetCandidates(
        [FromQuery] DateOnly? date,
        [FromQuery] string? stage,
        [FromQuery] bool? actionableOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await reversalBounce.GetCandidatesAsync(date, stage, actionableOnly, page, pageSize, cancellationToken));

    /// <summary>Chi tiết + lịch sử stage/điểm của một mã.</summary>
    [HttpGet("candidates/{symbol}")]
    [ProducesResponseType(typeof(ReversalBounceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReversalBounceDetailDto>> GetBySymbol(
        string symbol,
        [FromQuery] int lookback = 30,
        CancellationToken cancellationToken = default)
    {
        var detail = await reversalBounce.GetBySymbolAsync(symbol, lookback, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Backtest chiến lược sóng hồi trên OHLCV lịch sử (fill T+1, bán từ T+3, mô phỏng chất sàn).</summary>
    [HttpPost("backtest/run")]
    [ProducesResponseType(typeof(ReversalBounceBacktestReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReversalBounceBacktestReport>> RunBacktest(
        [FromBody] ReversalBounceBacktestRequest request,
        CancellationToken cancellationToken) =>
        Ok(await backtest.RunAsync(request, cancellationToken));

    /// <summary>Phase 1 — Shadow report: đo hiệu quả các tín hiệu đã lưu (không alert) trên OHLCV forward.</summary>
    [HttpGet("shadow-report")]
    [ProducesResponseType(typeof(ReversalBounceShadowSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReversalBounceShadowSummary>> ShadowReport(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] bool allowDefensiveEarlyExit = false,
        CancellationToken cancellationToken = default) =>
        Ok(await shadow.RunAsync(from, to, allowDefensiveEarlyExit, cancellationToken));
}
