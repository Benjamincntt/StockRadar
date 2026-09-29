using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/kich-ban")]
[Produces("application/json")]
[Tags("Kịch bản")]
public sealed class KichBanController(
    IPha1TruocPhienService pha1TruocPhien,
    IXepHangCoHoi xepHangCoHoi) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/kich-ban/xep-hang — lấy Top N cơ hội đã xếp hạng (chỉ các mã TRIGGERED).
    /// Chạy Pha 1 để lấy trạng thái kịch bản, lọc các kịch bản ĐÃ KÍCH HOẠT rồi xếp hạng 6 tiêu chí.
    /// </summary>
    [HttpGet("xep-hang")]
    [ProducesResponseType(typeof(IReadOnlyList<XepHangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<XepHangDto>>> XepHang(CancellationToken cancellationToken)
    {
        var pha1 = await pha1TruocPhien.ChayAsync(cancellationToken);

        var daKichHoat = pha1.KetQua
            .Where(k => k.TrangThai == TrangThaiKichBan.DaKichHoat)
            .ToList();

        var ketQua = await xepHangCoHoi.XepHangAsync(daKichHoat, cancellationToken);

        return Ok(ketQua.Select(XepHangDto.From).ToList());
    }
}
