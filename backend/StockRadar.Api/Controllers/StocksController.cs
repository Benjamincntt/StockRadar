using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/stocks")]
[Produces("application/json")]
[Tags("Stocks")]
public sealed class StocksController(
    IStockService stocks,
    ISectorCatalogService sectors,
    IStockLookupService lookup,
    IDichVuSuKienQuyen suKienQuyen,
    IV3HieuQuaService v3HieuQua) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<StockSearchHitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockSearchHitDto>>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await lookup.SearchAsync(q, limit, cancellationToken));

    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDetailDto>> GetBySymbol(
        string symbol,
        CancellationToken cancellationToken)
    {
        var detail = await stocks.GetDetailAsync(symbol, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("{symbol}/chart")]
    [ProducesResponseType(typeof(StockChartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockChartDto>> GetChart(
        string symbol,
        [FromQuery] string interval = "1D",
        CancellationToken cancellationToken = default)
    {
        var chart = await stocks.GetChartAsync(symbol, interval, cancellationToken);
        return chart is null ? NotFound() : Ok(chart);
    }

    /// <summary>
    /// GET /api/v1/stocks/{symbol}/v3 — các tín hiệu V3 (kịch bản P1++) của một mã, mới nhất trước.
    /// Trả mảng rỗng nếu mã chưa có tín hiệu V3.
    /// </summary>
    [HttpGet("{symbol}/v3")]
    [ProducesResponseType(typeof(IReadOnlyList<TinHieuV3Dto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TinHieuV3Dto>>> GetV3(
        string symbol,
        CancellationToken cancellationToken) =>
        Ok(await v3HieuQua.LayTinHieuTheoSymbolAsync(symbol.ToUpperInvariant(), cancellationToken));

    [HttpPatch("{symbol}/sector")]
    [ProducesResponseType(typeof(StockSectorUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockSectorUpdateResultDto>> UpdateSector(
        string symbol,
        [FromBody] UpdateStockSectorRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sectors.UpdateStockSectorAsync(symbol, request, cancellationToken));

    [HttpGet("{symbol}/rights-events")]
    [ProducesResponseType(typeof(IReadOnlyList<BanGhiSuKienQuyenDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<BanGhiSuKienQuyenDto>> LaySuKienQuyen(string symbol) =>
        Ok(suKienQuyen.LayTheoMa(symbol));

    [HttpPost("{symbol}/rights-events")]
    [ProducesResponseType(typeof(BanGhiSuKienQuyenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<BanGhiSuKienQuyenDto> ThemSuKienQuyen(
        string symbol,
        [FromBody] ThemSuKienQuyenRequest request) =>
        Ok(suKienQuyen.Them(symbol, request));
}
