using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.ChiaQuyen;

/// <summary>
/// Cổng chia chác: mã có NGÀY KHÔNG HƯỞNG QUYỀN trong cửa sổ tới hạn phải bị loại khỏi Top
/// (GateFailure = "Chờ chốt quyền ..."), bất kể các gate khác. Đặt đầu tiên nên luôn thắng.
/// </summary>
public sealed class GateChiaQuyenTests
{
    private static readonly ISignalAnalyzer Signals = new SignalAnalyzer();
    private static readonly SmartMoneySettings Settings = new(MinHistoryDays: 21);
    private static readonly BasePriceFilterSettings Runup = new();

    private static Stock MakeStock(string symbol, string sector)
    {
        var bars = new List<OhlcvBar>();
        var date = new DateOnly(2026, 6, 1);
        var close = 47_000m;
        for (var i = 0; i < 40; i++)
            bars.Add(new OhlcvBar(date.AddDays(i), close, close * 1.01m, close * 0.99m, close, 500_000));

        var last = bars[^1];
        var newClose = Math.Round(last.Close * 1.06m, 2);
        bars.Add(new OhlcvBar(
            date.AddDays(40), last.Close, newClose * 1.005m, last.Close * 0.995m, newClose, 1_000_000));
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

    [Fact]
    public void MaSapChotQuyen_BiLoaiKhoiTop()
    {
        var engine = new BuyDecisionEngine(Signals);
        var selector = new SmartMoneyOpportunitySelector(Signals, engine);
        var stock = MakeStock("DGW", "Bán lẻ");
        var ctx = selector.BuildContext(new[] { stock }, FlatIndex(), Runup, Settings);

        var coExDate = ctx with
        {
            NextExDateBySymbol = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
            {
                ["DGW"] = new DateOnly(2026, 10, 5)
            }
        };

        var ketQua = engine.Evaluate(stock, coExDate);
        Assert.False(ketQua.PassesTopFilter);
        Assert.NotNull(ketQua.GateFailure);
        Assert.Contains("chốt quyền", ketQua.GateFailure!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("05/10", ketQua.GateFailure!, StringComparison.Ordinal);
    }

    [Fact]
    public void KhongCoLichQuyen_KhongBiChotChac()
    {
        var engine = new BuyDecisionEngine(Signals);
        var selector = new SmartMoneyOpportunitySelector(Signals, engine);
        var stock = MakeStock("DGW", "Bán lẻ");
        var ctx = selector.BuildContext(new[] { stock }, FlatIndex(), Runup, Settings);

        // Không nạp map (fail-open) → không được phép báo lỗi chia chác.
        var ketQua = engine.Evaluate(stock, ctx);
        Assert.DoesNotContain("chốt quyền", ketQua.GateFailure ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
