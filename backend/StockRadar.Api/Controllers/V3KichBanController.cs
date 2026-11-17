using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

/// <summary>
/// API truy vấn hiệu quả bộ kịch bản V3 (spec 012, Phụ lục H) cho màn "Hiệu quả" web/mobile.
/// Chỉ đọc ViTheV3/TinHieuV3 qua <see cref="IV3HieuQuaService"/> — không tính lại chỉ báo.
/// </summary>
[ApiController]
[Route("api/v1/v3")]
[Produces("application/json")]
[Tags("Kịch bản V3")]
public sealed class V3KichBanController(IV3HieuQuaService hieuQua, IMuaTetService muaTet, IMuaTetTheoDoiService muaTetTheoDoi) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/v3/hieu-qua/tom-tat?tu=YYYY-MM-DD&amp;den=YYYY-MM-DD — tóm tắt theo kịch bản + danh mục.
    /// </summary>
    [HttpGet("hieu-qua/tom-tat")]
    [ProducesResponseType(typeof(HieuQuaTomTatV3Dto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HieuQuaTomTatV3Dto>> GetTomTat(
        [FromQuery] DateOnly? tu,
        [FromQuery] DateOnly? den,
        CancellationToken cancellationToken) =>
        Ok(await hieuQua.LayTomTatAsync(tu, den, cancellationToken));

    /// <summary>
    /// GET /api/v1/v3/hieu-qua/lich-su?page=1&amp;size=20&amp;status=mo|dong|huy — lịch sử lệnh V3 phân trang.
    /// </summary>
    [HttpGet("hieu-qua/lich-su")]
    [ProducesResponseType(typeof(ViTheV3PagedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ViTheV3PagedDto>> GetLichSu(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await hieuQua.LayLichSuAsync(page, size, status, cancellationToken));

    /// <summary>
    /// GET /api/v1/v3/mua-tet/gio — kịch bản "Mùa Tết — mã khỏe" (P4-19): 10 mã gần đỉnh 1 năm trong
    /// top 100 thanh khoản, tối đa 2 mã/ngành. Chỉ đọc nến ngày đã lưu, không gửi tin.
    /// </summary>
    [HttpGet("mua-tet/gio")]
    [ProducesResponseType(typeof(MuaTetGioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MuaTetGioDto>> GetMuaTetGio(CancellationToken cancellationToken) =>
        Ok(await muaTet.LayGioAsync(cancellationToken));

    /// <summary>
    /// GET /api/v1/v3/mua-tet/theo-doi — giá trị giỏ ĐÃ MUA theo <c>V3:MuaTet:GioDaMua</c> so với vốn,
    /// mức cảnh báo hiện tại (95% cảnh báo sớm, 90% bán cả giỏ). Chỉ đọc, không gửi tin.
    /// </summary>
    [HttpGet("mua-tet/theo-doi")]
    [ProducesResponseType(typeof(MuaTetTheoDoiDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MuaTetTheoDoiDto>> GetMuaTetTheoDoi(CancellationToken cancellationToken) =>
        Ok(await muaTetTheoDoi.LayTrangThaiAsync(cancellationToken));

    /// <summary>GET /api/v1/v3/hieu-qua/vi-the-dang-giu — các vị thế đang mở (chờ mua / đang giữ).</summary>
    [HttpGet("hieu-qua/vi-the-dang-giu")]
    [ProducesResponseType(typeof(IReadOnlyList<ViTheV3Dto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ViTheV3Dto>>> GetViTheDangGiu(
        CancellationToken cancellationToken) =>
        Ok(await hieuQua.LayViTheDangGiuAsync(cancellationToken));
}
