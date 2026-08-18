using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.SectorWave;

/// <summary>
/// Sóng ngành thay cho xếp hạng ngành top N: độ rộng tăng/giảm + lực + tiền vào + RS ngành.
/// </summary>
public sealed class SectorWaveTests
{
    private static readonly ISignalAnalyzer Signals = new SignalAnalyzer();

    private static readonly SmartMoneySettings Settings = new(
        MinHistoryDays: 21,
        MinAvgDailyVolume: 100_000m);

    private static readonly BasePriceFilterSettings Runup = new();

    /// <summary>Lịch sử phẳng 40 phiên rồi phiên cuối thay đổi <paramref name="lastChangePercent"/>.</summary>
    private static Stock MakeStock(
        string symbol,
        string sector,
        decimal lastChangePercent,
        decimal priorTrendPercentPerDay = 0m,
        long baseVolume = 500_000,
        decimal lastVolumeRatio = 1m)
    {
        var bars = new List<OhlcvBar>();
        var date = new DateOnly(2026, 6, 1);
        var close = 50_000m;
        for (var i = 0; i < 40; i++)
        {
            close *= 1m + priorTrendPercentPerDay / 100m;
            bars.Add(new OhlcvBar(date.AddDays(i), close, close * 1.01m, close * 0.99m, close, baseVolume));
        }

        var last = bars[^1];
        var newClose = Math.Round(last.Close * (1m + lastChangePercent / 100m), 2);
        bars.Add(new OhlcvBar(
            date.AddDays(40),
            last.Close,
            Math.Max(newClose, last.Close) * 1.005m,
            Math.Min(newClose, last.Close) * 0.995m,
            newClose,
            (long)(baseVolume * lastVolumeRatio)));

        return new Stock(symbol, symbol, sector, bars);
    }

    private static MarketIndex FlatIndex()
    {
        var bars = new List<OhlcvBar>();
        var date = new DateOnly(2026, 6, 1);
        for (var i = 0; i < 41; i++)
            bars.Add(new OhlcvBar(date.AddDays(i), 1_200m, 1_205m, 1_195m, 1_200m, 100_000_000));

        return new MarketIndex("VNINDEX", 1_200m, 0m, 50, MarketTrend.Sideway, 0m, bars);
    }

    private static SectorSnapshot WaveFor(IReadOnlyList<Stock> universe, string sector)
    {
        var selector = new SmartMoneyOpportunitySelector(Signals, new BuyDecisionEngine(Signals));
        var context = selector.BuildContext(universe, FlatIndex(), Runup, Settings);
        return context.SectorWaveFor(sector);
    }

    [Fact]
    public void DauKhiGanTranHetPhien_ThiCoSongNganhManh()
    {
        var universe = new List<Stock>
        {
            MakeStock("GAS", "Dầu khí", 6.8m, 0.3m, lastVolumeRatio: 2m),
            MakeStock("PLX", "Dầu khí", 6.5m, 0.3m, lastVolumeRatio: 2m),
            MakeStock("PVD", "Dầu khí", 5.2m, 0.3m, lastVolumeRatio: 2m),
            MakeStock("PVS", "Dầu khí", 4.4m, 0.3m, lastVolumeRatio: 2m),
        };

        var wave = WaveFor(universe, "Dầu khí");

        Assert.Equal(SectorWaveState.Strong, wave.Wave);
        Assert.Equal(4, wave.Advancers);
        Assert.Equal(0, wave.Decliners);
        Assert.Equal("4 tăng / 0 giảm", wave.BreadthDetail);
    }

    [Fact]
    public void NganhDoLuaChuaDuLucVaTien_ThiKhongCoSong()
    {
        var universe = new List<Stock>
        {
            MakeStock("AAA", "Nhựa", 0.3m),
            MakeStock("BBB", "Nhựa", -1.2m),
            MakeStock("CCC", "Nhựa", 0.4m),
            MakeStock("DDD", "Nhựa", -0.8m),
        };

        var wave = WaveFor(universe, "Nhựa");

        Assert.Equal(SectorWaveState.None, wave.Wave);
        Assert.False(wave.HasWave);
        Assert.Equal(2, wave.Advancers);
        Assert.Equal(2, wave.Decliners);
    }

    [Fact]
    public void DuDoRongNhungThieuTienVaoVaRs_ThiChiChomSong()
    {
        var universe = new List<Stock>
        {
            MakeStock("E1", "Bán lẻ", 2.1m, lastVolumeRatio: 0.9m),
            MakeStock("E2", "Bán lẻ", 1.9m, lastVolumeRatio: 0.9m),
            MakeStock("E3", "Bán lẻ", 1.6m, lastVolumeRatio: 0.9m),
            MakeStock("E4", "Bán lẻ", -0.5m, lastVolumeRatio: 0.9m),
        };

        var wave = WaveFor(universe, "Bán lẻ");

        Assert.Equal(SectorWaveState.Emerging, wave.Wave);
        Assert.True(wave.HasWave);
    }

    [Fact]
    public void NganhDuoiNguongSoMa_ThiCoiNhuKhongCoSong()
    {
        var universe = new List<Stock>
        {
            MakeStock("X1", "Ngành hiếm", 6.9m),
            MakeStock("X2", "Ngành hiếm", 6.8m),
        };

        var wave = WaveFor(universe, "Ngành hiếm");

        Assert.Equal(SectorWaveState.None, wave.Wave);
        Assert.Equal("Không đủ mã trong ngành", wave.BreadthDetail);
    }
}
