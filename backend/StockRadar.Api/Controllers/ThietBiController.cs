using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;

namespace StockRadar.Api.Controllers;

/// <summary>
/// Thiết bị nhận thông báo đẩy (spec 013). UserId chỉ lấy từ JWT qua ICurrentUserService — không bao giờ từ body.
/// </summary>
[ApiController]
[Route("api/v1/thiet-bi")]
[Produces("application/json")]
[Tags("ThietBi")]
[Authorize]
public sealed class ThietBiController(ThietBiService thietBi) : ControllerBase
{
    /// <summary>Đăng ký / cập nhật mã thiết bị; lặp cùng token vẫn 1 dòng.</summary>
    /// <param name="yeuCau">Mã thiết bị, nền tảng và mã cũ (tuỳ chọn).</param>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>Đã đăng ký và trạng thái kênh đẩy.</returns>
    [HttpPost("dang-ky")]
    [ProducesResponseType(typeof(DangKyThietBiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DangKyThietBiResponse>> DangKy([FromBody] DangKyThietBiRequest yeuCau,
        CancellationToken ct)
    {
        try
        {
            return Ok(await thietBi.DangKyAsync(yeuCau, ct));
        }
        catch (ThietBiValidationException loi)
        {
            return BadRequest(new ProblemDetails { Title = "Mã thiết bị không hợp lệ", Status = 400, Detail = loi.Message });
        }
    }

    /// <summary>Gỡ mã thiết bị; token lạ vẫn 204.</summary>
    /// <param name="yeuCau">Mã thiết bị cần gỡ.</param>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>204 khi đã xử lý xong.</returns>
    [HttpPost("go")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Go([FromBody] GoThietBiRequest yeuCau, CancellationToken ct)
    {
        try
        {
            // SECURITY: go xoá theo token, không kiểm chủ — ai biết token máy khác thì gỡ được máy đó khỏi danh sách nhận tin, không lộ dữ liệu.
            await thietBi.GoAsync(yeuCau, ct);
            return NoContent();
        }
        catch (ThietBiValidationException loi)
        {
            return BadRequest(new ProblemDetails { Title = "Mã thiết bị không hợp lệ", Status = 400, Detail = loi.Message });
        }
    }

    /// <summary>Gửi tin thử tới các máy của user hiện tại; 503 khi kênh đẩy chưa bật.</summary>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>Số máy đã gửi, hoặc 503 khi thiếu cấu hình Firebase.</returns>
    [HttpPost("gui-thu")]
    [ProducesResponseType(typeof(GuiThuResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GuiThuResponse>> GuiThu(CancellationToken ct)
    {
        var ketQua = await thietBi.GuiThuAsync(ct);
        if (ketQua.KenhDayTat)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Type = "https://httpstatuses.com/503",
                Title = "Kênh đẩy chưa bật",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Chưa cấu hình Firebase trên server, không gửi được thông báo thử."
            });
        return Ok(ketQua.KetQua);
    }
}
