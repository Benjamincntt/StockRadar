using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Service chạy Pha 3 — Đo lường outcome (Scenario Engine V2).
/// Sau T+3 phiên kể từ khi kịch bản kích hoạt, đo giá thoát và phân loại Thắng/Thua/Ngang.
/// </summary>
public interface IPha3DoLuongService
{
    /// <summary>Chạy một lần đo lường outcome cho các kịch bản đã kích hoạt nhưng chưa đo.</summary>
    Task<Pha3KetQuaDto> RunAsync(CancellationToken cancellationToken = default);
}
