namespace StockRadar.Domain.Services;

/// <summary>Pha chu kỳ Wyckoff (đơn giản hóa từ giá + volume).</summary>
public enum WyckoffPhase
{
    Unknown,
    Accumulation,
    Markup,
    Distribution,
    Markdown
}

public enum MarketWyckoffPhase
{
    Unknown,
    Favorable,
    Neutral,
    Unfavorable
}

/// <summary>Độ siết MA stack theo pha thị trường.</summary>
public enum MaStackStrictness
{
    Off,
    Loose,
    Medium,
    Full
}
