namespace StockRadar.Application.DTOs;

/// <summary>Đăng ký mã thiết bị nhận thông báo đẩy; UserId KHÔNG nằm trong body — luôn lấy từ JWT.</summary>
public sealed record DangKyThietBiRequest(string? Token, string? NenTang, string? TokenCu);

public sealed record GoThietBiRequest(string? Token);

public sealed record DangKyThietBiResponse(bool DaDangKy, bool MaHoaThongBao);

public sealed record GuiThuResponse(bool GuiDuoc, int SoThietBi);

/// <summary>Kết quả gửi thử; KenhDayTat ⇒ controller trả 503 theo contracts/thiet-bi-api.md.</summary>
public sealed record GuiThuKetQua(bool KenhDayTat, GuiThuResponse? KetQua);
