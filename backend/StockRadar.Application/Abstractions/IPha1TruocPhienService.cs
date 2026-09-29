using StockRadar.Domain.Services;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Service chạy Pha 1 — Trước phiên (Scenario Engine V2).
/// Sơ tuyển → đánh giá Bối cảnh + Hình thái cho từng mã → lưu trạng thái WATCHING/FORMING.
/// </summary>
public interface IPha1TruocPhienService
{
    /// <summary>Chạy toàn bộ Pha 1 và trả về thống kê kết quả.</summary>
    Task<Pha1KetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>Kết quả một lần chạy Pha 1 (sơ tuyển + danh sách kịch bản theo trạng thái).</summary>
public record Pha1KetQua(
    KetQuaSoTuyen SoTuyen,
    IReadOnlyList<KetQuaKichBan> KetQua,
    int SoDangHinhThanh,
    int SoDangTheoDoi);
