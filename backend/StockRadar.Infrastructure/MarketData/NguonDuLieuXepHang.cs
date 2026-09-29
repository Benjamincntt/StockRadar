using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nguồn dữ liệu xếp hạng cơ hội V2 — đọc từ các repository hiện có.
/// - Sector + MarketPhase: lấy từ snapshot Top cơ hội mới nhất của mã (<see cref="IDailyOpportunityRepository"/>).
/// - Sóng ngành: tra trạng thái regime theo sector (<see cref="ISectorWaveRegimeRepository"/>).
/// - RS percentile: chưa có store per-symbol truy vấn được → trả null (service sẽ dùng mặc định 50).
/// </summary>
public sealed class NguonDuLieuXepHang : INguonDuLieuXepHang
{
    private readonly IDailyOpportunityRepository _coHoi;
    private readonly ISectorWaveRegimeRepository _songNganh;

    public NguonDuLieuXepHang(
        IDailyOpportunityRepository coHoi,
        ISectorWaveRegimeRepository songNganh)
    {
        _coHoi = coHoi;
        _songNganh = songNganh;
    }

    /// <inheritdoc />
    /// <remarks>
    /// TODO: nối nguồn RS percentile per-symbol khi có store phù hợp (DailyOpportunityRecord chưa lưu RS).
    /// Hiện trả null → <c>XepHangCoHoiService</c> dùng điểm trung tính 50.
    /// </remarks>
    public Task<decimal?> LayRsPercentileAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult<decimal?>(null);

    /// <inheritdoc />
    public async Task<string?> LayTrangThaiNganhAsync(string symbol, CancellationToken ct = default)
    {
        var record = await _coHoi.GetBySymbolAsync(symbol, null, ct);
        if (record is null || string.IsNullOrWhiteSpace(record.Sector))
            return null;

        var regime = await _songNganh.GetLatestAsync(record.Sector, ct);
        if (regime is null)
            return null;

        // Regime xuyên phiên chỉ có IsActive → map Active/None (chưa phân biệt Emerging).
        return regime.IsActive ? "Active" : "None";
    }

    /// <inheritdoc />
    public async Task<string> LayPhaThiTruongAsync(CancellationToken ct = default)
    {
        var latestDate = await _coHoi.GetLatestForDateAsync(ct);
        if (latestDate is null)
            return "Neutral";

        var records = await _coHoi.GetForDateAsync(latestDate.Value, ct);
        var phase = records.FirstOrDefault()?.MarketPhase;
        if (string.IsNullOrWhiteSpace(phase))
            return "Neutral";

        return phase;
    }
}
