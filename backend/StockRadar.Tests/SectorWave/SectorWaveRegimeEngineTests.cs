using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.SectorWave;

/// <summary>
/// Spec 007 — Sector Wave State Engine: tách "Xung lực phiên" (ClassifyWave, per-phiên) khỏi
/// "Sóng ngành" (regime giữ Active nhiều phiên cho tới khi cạn volume 3 phiên liên tiếp hoặc hết TTL).
/// </summary>
public sealed class SectorWaveRegimeEngineTests
{
    private const string Sector = "Chứng khoán";

    private static readonly SectorWaveSettings Settings = new(
        FailureMaxVolumeRatio: 0.5m,
        FailureConsecutiveSessions: 3,
        MaxActiveSessions: 20);

    private static readonly ISectorWaveRegimeEngine Engine = new SectorWaveRegimeEngine();

    private static SectorSnapshot Snapshot(SectorWaveState wave, decimal volumeRatio = 1.5m) =>
        new(Sector, 5, 3, 2, 0.6m, 2m, 0.3m, volumeRatio, 1m, 1m, 1_000_000m, wave);

    private static DateOnly D(int day) => new(2026, 8, day);

    [Fact]
    public void PhienDauCoSong_ThiKichHoatActive()
    {
        var result = Engine.Advance(Sector, previous: null, Snapshot(SectorWaveState.Strong), D(21), Settings);

        Assert.True(result.IsActive);
        Assert.Equal(D(21), result.ActivatedOn);
        Assert.Equal(1, result.SessionsSinceActivation);
        Assert.Equal(0, result.ConsecutiveLowVolumeSessions);
        Assert.Null(result.FailedOn);
    }

    [Fact]
    public void ChuaTungActive_KhongCoSongHomNay_ThiVanInactive()
    {
        var result = Engine.Advance(Sector, previous: null, Snapshot(SectorWaveState.None), D(21), Settings);

        Assert.False(result.IsActive);
        Assert.Null(result.FailedOn);
    }

    [Fact]
    public void DangActive_PhienNghiVolumeBinhThuong_ThiVanGiuActive()
    {
        var activated = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(21), Settings);
        var paused = Engine.Advance(Sector, activated, Snapshot(SectorWaveState.None, 1.0m), D(22), Settings);

        Assert.True(paused.IsActive);
        Assert.Equal(D(21), paused.ActivatedOn);
        Assert.Equal(2, paused.SessionsSinceActivation);
        Assert.Equal(0, paused.ConsecutiveLowVolumeSessions);
    }

    [Fact]
    public void Du3PhienLienTiepVolumeThap_ThiGaySong()
    {
        var state = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(21), Settings);
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.4m), D(22), Settings);
        Assert.True(state.IsActive);
        Assert.Equal(1, state.ConsecutiveLowVolumeSessions);

        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.3m), D(25), Settings);
        Assert.True(state.IsActive);
        Assert.Equal(2, state.ConsecutiveLowVolumeSessions);

        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.45m), D(26), Settings);

        Assert.False(state.IsActive);
        Assert.Equal(3, state.ConsecutiveLowVolumeSessions);
        Assert.Equal(D(26), state.FailedOn);
    }

    [Fact]
    public void ChuoiVolumeThapBiNgatBoiPhienBinhThuong_ThiKhongGaySong()
    {
        var state = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(21), Settings);
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.4m), D(22), Settings);
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.45m), D(25), Settings);
        Assert.Equal(2, state.ConsecutiveLowVolumeSessions);

        // Phiên kế tiếp volume phục hồi (>=0.5) — chuỗi phải reset về 0, không gãy sóng.
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.9m), D(26), Settings);

        Assert.True(state.IsActive);
        Assert.Equal(0, state.ConsecutiveLowVolumeSessions);
    }

    [Fact]
    public void TaiXacNhanGiuaChuoiVolumeThap_ThiResetVaKichHoatChuKyMoi()
    {
        var state = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(21), Settings);
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 0.4m), D(22), Settings);
        Assert.Equal(1, state.ConsecutiveLowVolumeSessions);

        // Phiên này vừa đạt ClassifyWave Emerging vừa volume thấp — ưu tiên tái xác nhận (FR-002 trước FR-004).
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.Emerging, 0.4m), D(25), Settings);

        Assert.True(state.IsActive);
        Assert.Equal(D(25), state.ActivatedOn);
        Assert.Equal(1, state.SessionsSinceActivation);
        Assert.Equal(0, state.ConsecutiveLowVolumeSessions);
    }

    [Fact]
    public void VuotTtl_KhongTaiXacNhanKhongGaySong_ThiHetHanActive()
    {
        var state = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(1), Settings);
        for (var i = 2; i <= 19; i++)
            state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 1.0m), D(i), Settings);

        Assert.True(state.IsActive);
        Assert.Equal(19, state.SessionsSinceActivation);

        // Phiên thứ 20 kể từ kích hoạt (MaxActiveSessions) — hết hạn an toàn, không cần gãy sóng.
        state = Engine.Advance(Sector, state, Snapshot(SectorWaveState.None, 1.0m), D(20), Settings);

        Assert.False(state.IsActive);
        Assert.Equal(20, state.SessionsSinceActivation);
        Assert.Equal(D(20), state.FailedOn);
    }

    [Fact]
    public void DaInactive_KhongCoSongHomNay_ThiGiuNguyenInactiveVaActivatedOnCu()
    {
        var active = Engine.Advance(Sector, null, Snapshot(SectorWaveState.Strong), D(1), Settings);
        var failed = active;
        for (var i = 2; i <= 4; i++)
            failed = Engine.Advance(Sector, failed, Snapshot(SectorWaveState.None, 0.1m), D(i), Settings);
        Assert.False(failed.IsActive);

        var next = Engine.Advance(Sector, failed, Snapshot(SectorWaveState.None, 1.0m), D(5), Settings);

        Assert.False(next.IsActive);
        Assert.Equal(failed.ActivatedOn, next.ActivatedOn);
        Assert.Equal(failed.FailedOn, next.FailedOn);
    }
}
