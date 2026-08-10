namespace StockRadar.Domain.MasterAlerts;

public static class MasterAlertExitRegimes
{
    public const string UnderBase = "UnderBase";
    public const string BlueSky = "BlueSky";

    public static bool IsUnderBase(string? regime) =>
        string.Equals(regime, UnderBase, StringComparison.OrdinalIgnoreCase);

    public static bool IsBlueSky(string? regime) =>
        string.IsNullOrWhiteSpace(regime)
        || string.Equals(regime, BlueSky, StringComparison.OrdinalIgnoreCase);
}
