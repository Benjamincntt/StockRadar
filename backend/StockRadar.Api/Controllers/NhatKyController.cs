using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;

namespace StockRadar.Api.Controllers;

/// <summary>
/// Nhật ký giao dịch thật (spec 010).
/// SECURITY: [Authorize] bắt buộc; UserId chỉ lấy từ JWT qua ICurrentUserService — không bao giờ từ body/query (N6/FR-009).
/// </summary>
[ApiController]
[Route("api/v1/nhat-ky")]
[Produces("application/json")]
[Tags("NhatKy")]
[Authorize]
public sealed class NhatKyController(NhatKyService nhatKy, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Danh sách lệnh của user hiện tại, mới nhất trước.</summary>
    [HttpGet("lenh")]
    [ProducesResponseType(typeof(NhatKyDanhSachResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NhatKyDanhSachResponse>> GetDanhSach(
        [FromQuery] string? symbol,
        [FromQuery] DateTime? tu,
        [FromQuery] DateTime? den,
        [FromQuery] string? loaiLenh,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
    {
        if (loaiLenh is not (null or "" or "Mua" or "Ban" or "DieuChinh"))
            return BadRequest(new { detail = "loaiLenh phải là Mua, Ban hoặc DieuChinh." });
        return Ok(await nhatKy.LayDanhSachAsync(currentUser.UserId, symbol, tu, den, loaiLenh, page, size, ct));
    }

    [HttpGet("lenh/{id:guid}")]
    [ProducesResponseType(typeof(LenhDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LenhDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await nhatKy.LayMotAsync(currentUser.UserId, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Thêm lệnh. 400 thiếu giá/KL; 422 bán quá vị thế (Q7 — không lưu).</summary>
    [HttpPost("lenh")]
    [ProducesResponseType(typeof(LenhDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LenhDto>> Create([FromBody] TaoLenhRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await nhatKy.TaoLenhAsync(currentUser.UserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (Exception ex) when (ex is NhatKyValidationException or NhatKyVuotBanException or NhatKyTinHieuNotFoundException)
        {
            return MapNhatKyError(ex);
        }
    }

    [HttpPut("lenh/{id:guid}")]
    [ProducesResponseType(typeof(LenhDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LenhDto>> Update(Guid id, [FromBody] TaoLenhRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await nhatKy.SuaLenhAsync(currentUser.UserId, id, request, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (Exception ex) when (ex is NhatKyValidationException or NhatKyVuotBanException or NhatKyTinHieuNotFoundException)
        {
            return MapNhatKyError(ex);
        }
    }

    [HttpDelete("lenh/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await nhatKy.XoaLenhAsync(currentUser.UserId, id, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Vị thế theo mã — projection on-the-fly từ lệnh (FR-004); US3 gồm cả vị thế đã đóng + lãi/lỗ ròng.</summary>
    [HttpGet("vi-the")]
    [ProducesResponseType(typeof(IReadOnlyList<ViTheDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ViTheDto>>> GetViThe(CancellationToken ct) =>
        Ok(await nhatKy.LayViTheAsync(currentUser.UserId, ct));

    /// <summary>Tổng hợp tài khoản thật theo kỳ (FR-006) — ky ∈ week|month|quarter|all, mặc định month ( convention HieuQuaController).</summary>
    [HttpGet("thong-ke")]
    [ProducesResponseType(typeof(ThongKeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ThongKeDto>> GetThongKe([FromQuery] string ky = "month", CancellationToken ct = default) =>
        Ok(await nhatKy.LayThongKeAsync(currentUser.UserId, ky, ct));

    /// <summary>Picker tín hiệu V2 (kịch bản đã kích hoạt) — chỉ đọc, mới nhất trước.</summary>
    [HttpGet("tin-hieu/v2")]
    [ProducesResponseType(typeof(TinHieuPickerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TinHieuPickerResponse>> GetTinHieuV2(
        [FromQuery] DateTime? tu,
        [FromQuery] DateTime? den,
        [FromQuery] int page = 1,
        [FromQuery] int size = 30,
        CancellationToken ct = default) =>
        Ok(await nhatKy.LayTinHieuV2Async(currentUser.UserId, tu, den, page, size, ct));

    /// <summary>Đánh dấu tín hiệu bỏ qua / không khớp (Q3) — upsert idempotent, 404 nếu tín hiệu không tồn tại.</summary>
    [HttpPut("tin-hieu/{nguon}/{maTinHieu}/bo-qua")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DatBoQua(string nguon, string maTinHieu, [FromBody] BoQuaRequest? request, CancellationToken ct)
    {
        try
        {
            await nhatKy.DatBoQuaAsync(currentUser.UserId, nguon, maTinHieu, request?.LyDoBoQua, ct);
            return NoContent();
        }
        catch (Exception ex) when (ex is NhatKyTinHieuNotFoundException or NhatKyValidationException)
        {
            return MapNhatKyError(ex);
        }
    }

    [HttpDelete("tin-hieu/{nguon}/{maTinHieu}/bo-qua")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> XoaBoQua(string nguon, string maTinHieu, CancellationToken ct)
    {
        try
        {
            await nhatKy.XoaBoQuaAsync(currentUser.UserId, nguon, maTinHieu, ct);
            return NoContent();
        }
        catch (Exception ex) when (ex is NhatKyTinHieuNotFoundException or NhatKyValidationException)
        {
            return MapNhatKyError(ex);
        }
    }

    /// <summary>Mapping lỗi nghiệp vụ nhật ký theo shapes contracts/nhat-ky-api.md (middleware chỉ biết AppException).</summary>
    private ObjectResult MapNhatKyError(Exception ex) => ex switch
    {
        NhatKyValidationException val => (ObjectResult)BadRequest(new { detail = val.Message }),
        NhatKyVuotBanException vuot => (ObjectResult)UnprocessableEntity(new { detail = vuot.Message, khoLuongHienCo = vuot.KhoiLuongHienCo }),
        NhatKyTinHieuNotFoundException => StatusCode(StatusCodes.Status404NotFound, null),
        _ => throw ex,
    };
}
