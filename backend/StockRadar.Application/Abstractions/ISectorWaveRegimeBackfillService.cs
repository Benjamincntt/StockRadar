using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Backfill một lần trạng thái Sóng ngành (spec 007) từ lịch sử OHLCV đã có sẵn, tính đúng
/// point-in-time (không nhìn thấy dữ liệu tương lai) — KHÔNG đụng Buy Score/Top/DailyOpportunities.
/// </summary>
public interface ISectorWaveRegimeBackfillService
{
    Task<SectorWaveRegimeBackfillResultDto> RunAsync(DateOnly fromDate, CancellationToken cancellationToken = default);
}
