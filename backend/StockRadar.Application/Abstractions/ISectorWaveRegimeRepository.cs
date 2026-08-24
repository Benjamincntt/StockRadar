using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

/// <summary>Lưu/đọc trạng thái Sóng ngành xuyên phiên (spec 007) — idempotent theo (Sector, TradingDate).</summary>
public interface ISectorWaveRegimeRepository
{
    Task UpsertAsync(SectorWaveRegimeState state, CancellationToken cancellationToken = default);

    /// <summary>Bản ghi gần nhất của ngành (bất kể có gap phiên hay không) — dùng làm "previous" khi advance.</summary>
    Task<SectorWaveRegimeState?> GetLatestAsync(string sector, CancellationToken cancellationToken = default);
}
