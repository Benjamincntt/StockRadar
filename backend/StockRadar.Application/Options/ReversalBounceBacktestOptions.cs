namespace StockRadar.Application.Options;

public sealed class ReversalBounceBacktestOptions
{
    public const string SectionName = "ReversalBounceBacktest";

    public int MaxSetupsToSimulate { get; set; } = 10_000;
    public int MaxSignalsPerDay { get; set; } = 5;
    public int DefaultMinTradingSessionsToSell { get; set; } = 3;
    public int DefaultTimeStopSessions { get; set; } = 10;
    public int DefaultMaxHoldSessions { get; set; } = 20;
}
