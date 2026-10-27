using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>V3 trước phiên (08:30) — nhịp nhẹ: chỉ nạp các mã UNI đã lưu + MA50 đọc từ chuỗi đã lưu,
/// rồi chấm KB1/KB2 trên đóng cửa phiên qua → tin mua ATO phiên nay (Phụ lục D).</summary>
public interface IV3TruocPhienService
{
    Task<V3ChayKetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>V3 trong phiên (mỗi 1 phút) — nhịp nhẹ nhất: CHỈ lấy giá các mã đang giữ, so với ngưỡng đã lưu
/// (không nạp lịch sử, không gọi LayCacMaAsync); khớp mua mở cửa + xét bán realtime.</summary>
public interface IV3TrongPhienService
{
    Task<V3ChayKetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>V3 ATC (~14:25) — nhịp nhẹ: nạp UNI đã lưu + giá realtime, chỉ số cộng điểm hôm nay;
/// chấm KB4/N1/N3 → tin mua ATC; xử lý vị thế hết hạn → bán ATC.</summary>
public interface IV3AtcService
{
    Task<V3ChayKetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>V3 sau phiên (16:00) — nhịp nạp đầy đủ duy nhất: lưu chỉ số hôm nay + UNI phiên sau vào
/// ChiSoNgayV3, tính sẵn ngưỡng bán/giá tham chiếu cho vị thế đang giữ (Phụ lục D).</summary>
public interface IV3SauPhienService
{
    Task<V3ChayKetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>
/// Lần nạp MỘT LẦN khi deploy: dựng chuỗi chỉ số + UNI theo ngày từ 01/2023 vào <c>ChiSoNgayV3</c>.
/// KHÔNG tạo tín hiệu hay vị thế cho các ngày đã qua.
/// </summary>
public interface IV3NapChiSoBackfillService
{
    Task<V3NapChiSoKetQua> NapAsync(DateOnly? tuNgay = null, CancellationToken ct = default);
}

/// <summary>Truy vấn hiệu quả V3 cho web/mobile (Phụ lục H). Chỉ đọc ViTheV3/TinHieuV3.</summary>
public interface IV3HieuQuaService
{
    Task<HieuQuaTomTatV3Dto> LayTomTatAsync(DateOnly? tu, DateOnly? den, CancellationToken ct = default);
    Task<ViTheV3PagedDto> LayLichSuAsync(int page, int size, string? status, CancellationToken ct = default);
    Task<IReadOnlyList<ViTheV3Dto>> LayViTheDangGiuAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TinHieuV3Dto>> LayTinHieuTheoSymbolAsync(string symbol, CancellationToken ct = default);
}
