using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Tính trạng thái sóng ngành xuyên phiên (spec 007) — tách "Xung lực phiên" (ClassifyWave,
/// per-phiên) khỏi "Sóng ngành" (regime giữ Active nhiều phiên cho tới khi cạn volume/hết hạn).
/// </summary>
public interface ISectorWaveRegimeEngine
{
    /// <summary>
    /// Tính trạng thái mới cho một phiên, dựa trên trạng thái phiên trước (nếu có) và ảnh chụp
    /// sóng ngành (<see cref="SectorSnapshot"/>) của đúng phiên đang phân tích.
    /// </summary>
    SectorWaveRegimeState Advance(
        string sector,
        SectorWaveRegimeState? previous,
        SectorSnapshot todaySnapshot,
        DateOnly tradingDate,
        SectorWaveSettings settings);
}

public sealed class SectorWaveRegimeEngine : ISectorWaveRegimeEngine
{
    public SectorWaveRegimeState Advance(
        string sector,
        SectorWaveRegimeState? previous,
        SectorSnapshot todaySnapshot,
        DateOnly tradingDate,
        SectorWaveSettings settings)
    {
        // FR-002: phiên đạt Strong/Emerging luôn (tái) kích hoạt một chu kỳ Active mới,
        // ưu tiên trước điều kiện gãy sóng (edge case: volume thấp cùng phiên vẫn được tái xác nhận).
        if (todaySnapshot.HasWave)
            return new SectorWaveRegimeState(
                sector,
                tradingDate,
                IsActive: true,
                ActivatedOn: tradingDate,
                SessionsSinceActivation: 1,
                ConsecutiveLowVolumeSessions: 0,
                FailedOn: null);

        if (previous is null || !previous.IsActive)
            return new SectorWaveRegimeState(
                sector,
                tradingDate,
                IsActive: false,
                ActivatedOn: previous?.ActivatedOn ?? tradingDate,
                SessionsSinceActivation: 0,
                ConsecutiveLowVolumeSessions: 0,
                FailedOn: previous?.FailedOn);

        // Đang Active, phiên này không tái xác nhận — kiểm tra gãy sóng (FR-004) và TTL (FR-003).
        var isLowVolume = todaySnapshot.VolumeRatio < settings.FailureMaxVolumeRatio;
        var consecutiveLow = isLowVolume ? previous.ConsecutiveLowVolumeSessions + 1 : 0;
        var sessionsSinceActivation = previous.SessionsSinceActivation + 1;

        var brokenByLowVolume = consecutiveLow >= settings.FailureConsecutiveSessions;
        var expiredByTtl = sessionsSinceActivation >= settings.MaxActiveSessions;

        if (brokenByLowVolume || expiredByTtl)
            return new SectorWaveRegimeState(
                sector,
                tradingDate,
                IsActive: false,
                ActivatedOn: previous.ActivatedOn,
                SessionsSinceActivation: sessionsSinceActivation,
                ConsecutiveLowVolumeSessions: consecutiveLow,
                FailedOn: tradingDate);

        return new SectorWaveRegimeState(
            sector,
            tradingDate,
            IsActive: true,
            ActivatedOn: previous.ActivatedOn,
            SessionsSinceActivation: sessionsSinceActivation,
            ConsecutiveLowVolumeSessions: consecutiveLow,
            FailedOn: null);
    }
}
