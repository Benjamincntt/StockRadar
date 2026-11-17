namespace StockRadar.Application.DTOs;

/// <summary>Một mã trong giỏ "Mùa Tết — mã khỏe".</summary>
/// <param name="GiaDongCua">Giá đóng cửa phiên dữ liệu, nghìn đồng (cùng đơn vị DB).</param>
/// <param name="GanDinh">Đóng cửa ÷ giá cao nhất 250 phiên (0–1).</param>
/// <param name="GiaTriGiaoDichTb20Ty">Giá trị giao dịch TB 20 phiên, tỷ đồng.</param>
/// <param name="GiaMotLo">1 lô (100 cổ) × giá đóng cửa, đơn vị đồng.</param>
/// <param name="SoLoGoiY">Số lô gợi ý cho một chỗ vốn, tối thiểu 1.</param>
/// <param name="VuotCho">1 lô đắt hơn 1,5 lần giá trị một chỗ — chỉ cảnh báo, mã vẫn nằm trong giỏ.</param>
public sealed record MuaTetMaDto(
    int ThuTu,
    string Symbol,
    string Sector,
    decimal GiaDongCua,
    decimal GanDinh,
    decimal GiaTriGiaoDichTb20Ty,
    decimal GiaMotLo,
    int SoLoGoiY,
    bool VuotCho);

/// <summary>Giỏ "Mùa Tết — mã khỏe" (P4-19) của một ngày dữ liệu.</summary>
/// <param name="NgayDuLieu">Ngày của nến hoàn thành gần nhất được dùng.</param>
/// <param name="VonBanDau">Vốn cả giỏ giả định, đơn vị đồng.</param>
/// <param name="GiaTriMotCho">Vốn ÷ 10, đơn vị đồng.</param>
/// <param name="BiLoaiLanDonVi">Mã bị loại vì giá lưu lẫn đơn vị (nghìn đồng → đồng).</param>
public sealed record MuaTetGioDto(
    string Ten,
    DateOnly NgayDuLieu,
    decimal VonBanDau,
    decimal GiaTriMotCho,
    IReadOnlyList<MuaTetMaDto> CacMa,
    IReadOnlyList<string> BiLoaiLanDonVi);
