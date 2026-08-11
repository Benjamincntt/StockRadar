using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.SellExit;

public sealed class PreSellWindowMessageTests
{
    [Fact]
    public void RiskWarning_title_does_not_contain_Ban()
    {
        var row = SellExitFixtures.Row(96m);
        var body = VipTelegramMessageFormatter.FormatRiskWarning(
            "GAS",
            4.0m,
            -1.0m,
            row,
            "Chế độ: BlueSky\nRút từ đỉnh -4.0% so mốc 100\nP&L so entry -1%");

        var titleLine = body.Split('\n')[0];
        Assert.Contains("CẢNH BÁO RỦI RO", titleLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Bán", titleLine, StringComparison.Ordinal);
        Assert.Contains("chưa bán được", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BlueSky", body, StringComparison.Ordinal);
        Assert.Contains("rút từ đỉnh -4%", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P&L so entry -1%", body, StringComparison.OrdinalIgnoreCase);
    }
}
