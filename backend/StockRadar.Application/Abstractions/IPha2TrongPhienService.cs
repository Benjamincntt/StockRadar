using StockRadar.Domain.Services;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Service chạy Pha 2 — Trong phiên (Scenario Engine V2).
/// Mỗi 1 phút: kiểm tra cò kích hoạt cho các mã FORMING + kiểm tra sell signals cho HOLDING.
/// </summary>
public interface IPha2TrongPhienService
{
    /// <summary>Chạy một lần kiểm tra trigger trong phiên.</summary>
    Task<Pha2KetQua> ChayAsync(CancellationToken ct = default);
}

/// <summary>Kết quả một lần chạy Pha 2 (số trigger, số alert đã bắn, chi tiết).</summary>
public record Pha2KetQua(
    int SoTrigger,
    int SoAlert,
    IReadOnlyList<KetQuaKichBan> ChiTiet);
