using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Services;

/// <summary>
/// Nghiệp vụ thiết bị nhận thông báo đẩy (spec 013). UserId luôn lấy từ JWT qua ICurrentUserService, không bao giờ từ body.
/// </summary>
public sealed class ThietBiService(
    IThietBiNhanThongBaoRepository kho,
    INguiTinDay kenhDay,
    ICurrentUserService nguoiDung)
{
    private const string NenTangAndroid = "android";
    private const string KenhTinThu = "v3-signal";
    private const string TieuDeTinThu = "THỬ · JUICE";
    private const string NoiDungTinThu = "Thông báo thử từ app — nếu bạn thấy dòng này là kênh đẩy hoạt động.";

    /// <summary>Upsert mã thiết bị của user hiện tại; tokenCu chỉ bị xoá khi thuộc chính user này.</summary>
    /// <param name="yeuCau">Mã thiết bị, nền tảng và mã cũ (tuỳ chọn).</param>
    /// <param name="ct">Huỷ thao tác DB.</param>
    /// <returns>Đã đăng ký và trạng thái kênh đẩy trên server.</returns>
    /// <exception cref="ThietBiValidationException">Mã thiết bị hoặc nền tảng không hợp lệ.</exception>
    public async Task<DangKyThietBiResponse> DangKyAsync(DangKyThietBiRequest yeuCau,
        CancellationToken ct = default)
    {
        var token = ChuanHoaToken(yeuCau.Token);
        var nenTang = ChuanHoaNenTang(yeuCau.NenTang);
        var tokenCu = string.IsNullOrWhiteSpace(yeuCau.TokenCu) ? null : ChuanHoaToken(yeuCau.TokenCu);
        await kho.DangKyAsync(nguoiDung.UserId, token, nenTang, tokenCu, ct);
        return new DangKyThietBiResponse(true, kenhDay.DangHoatDong);
    }

    /// <summary>Gỡ mã thiết bị, idempotent — token không tồn tại vẫn thành công.</summary>
    /// <param name="yeuCau">Mã thiết bị cần gỡ.</param>
    /// <param name="ct">Huỷ thao tác DB.</param>
    /// <returns>Hoàn tất sau khi xoá.</returns>
    /// <exception cref="ThietBiValidationException">Mã thiết bị không hợp lệ.</exception>
    public async Task GoAsync(GoThietBiRequest yeuCau, CancellationToken ct = default) =>
        await kho.XoaAsync(ChuanHoaToken(yeuCau.Token), ct);

    /// <summary>Gửi tin thử tới đúng các máy của user hiện tại; không có tham số nào bắn sang user khác.</summary>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>Kênh đẩy tắt, hoặc số máy và kết quả gửi.</returns>
    public async Task<GuiThuKetQua> GuiThuAsync(CancellationToken ct = default)
    {
        if (!kenhDay.DangHoatDong)
            return new GuiThuKetQua(true, null);

        var cacToken = await kho.LayTokenTheoUserAsync(nguoiDung.UserId, ct);
        if (cacToken.Count == 0)
            return new GuiThuKetQua(false, new GuiThuResponse(false, 0));

        DayTinKetQua ketQua;
        try
        {
            ketQua = await kenhDay.GuiAsync(cacToken,
                new DayTinNhan(TieuDeTinThu, NoiDungTinThu, KenhTinThu, null), ct);
        }
        catch (Exception loi) when (loi is not OperationCanceledException)
        {
            // BUSINESS-RULE: Tin thử lỗi vận chuyển vẫn trả 200 với guiDuoc=false, không làm hỏng nút gửi thử.
            return new GuiThuKetQua(false, new GuiThuResponse(false, cacToken.Count));
        }

        if (ketQua.TokenHong.Count > 0)
            await kho.XoaCacTokenAsync(ketQua.TokenHong, ct);

        return new GuiThuKetQua(false, new GuiThuResponse(ketQua.SoDaGui > 0, cacToken.Count));
    }

    private static string ChuanHoaToken(string? token)
    {
        var ma = token?.Trim();
        if (string.IsNullOrEmpty(ma) || ma.Length is < 20 or > 512 || ma.Any(char.IsWhiteSpace))
            throw new ThietBiValidationException("Mã thiết bị không hợp lệ.");
        return ma;
    }

    private static string ChuanHoaNenTang(string? nenTang)
    {
        var tang = nenTang?.Trim();
        if (string.IsNullOrEmpty(tang))
            return NenTangAndroid;
        if (!string.Equals(tang, NenTangAndroid, StringComparison.OrdinalIgnoreCase))
            throw new ThietBiValidationException("Chỉ hỗ trợ nền tảng android.");
        return NenTangAndroid;
    }
}
