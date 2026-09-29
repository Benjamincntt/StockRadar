using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

/// <summary>
/// API truy vấn hiệu quả kịch bản V2 (Performance Tracking).
/// Cung cấp tóm tắt theo kỳ, lịch sử lệnh (phân trang) và chi tiết từng lệnh.
/// </summary>
[ApiController]
[Route("api/v1/hieu-qua")]
[Produces("application/json")]
[Tags("Hiệu quả")]
public sealed class HieuQuaController(IHieuQuaKichBanService hieuQua) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/hieu-qua/tom-tat?period=month — tóm tắt hiệu quả tổng hợp theo kỳ.
    /// </summary>
    /// <param name="period">"week" / "month" / "quarter" / "all". Mặc định "month".</param>
    [HttpGet("tom-tat")]
    [ProducesResponseType(typeof(HieuQuaTomTatDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HieuQuaTomTatDto>> GetTomTat(
        [FromQuery] string period = "month",
        CancellationToken ct = default)
    {
        var ketQua = await hieuQua.GetTomTatAsync(period, ct);
        return Ok(ketQua);
    }

    /// <summary>
    /// GET /api/v1/hieu-qua/lich-su?page=1&amp;size=20 — lịch sử lệnh có phân trang.
    /// </summary>
    [HttpGet("lich-su")]
    [ProducesResponseType(typeof(LichSuResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LichSuResponse>> GetLichSu(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await hieuQua.GetLichSuAsync(page, size, ct);
        return Ok(new LichSuResponse(items, totalCount, page, size));
    }

    /// <summary>
    /// GET /api/v1/hieu-qua/chi-tiet/{id} — chi tiết một lệnh kèm snapshot JSON lúc kích hoạt.
    /// </summary>
    [HttpGet("chi-tiet/{id:int}")]
    [ProducesResponseType(typeof(ChiTietLenhDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChiTietLenhDto>> GetChiTiet(int id, CancellationToken ct)
    {
        var chiTiet = await hieuQua.GetChiTietAsync(id, ct);
        return chiTiet is null ? NotFound() : Ok(chiTiet);
    }

    /// <summary>Khung phản hồi lịch sử lệnh có phân trang.</summary>
    public sealed record LichSuResponse(
        IReadOnlyList<LichSuLenhDto> Items,
        int TotalCount,
        int Page,
        int PageSize);
}
