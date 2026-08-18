namespace StockRadar.Domain.Enums;

/// <summary>
/// Playbook gán độc quyền cho từng mã trong một phiên, dùng làm chiều đo accuracy chỉ báo.
/// Thứ tự ưu tiên: BreakoutDarvas > PullbackMa20 > ReversalBounce > Unclassified.
/// Giá trị lưu DB là string id ổn định (breakout-darvas, …), không dùng giá trị int.
/// </summary>
public enum PlaybookId
{
    BreakoutDarvas,
    PullbackMa20,
    ReversalBounce,
    Unclassified,
    Legacy,
}

public static class PlaybookIdExtensions
{
    public static string ToStringId(this PlaybookId playbook) => playbook switch
    {
        PlaybookId.BreakoutDarvas  => "breakout-darvas",
        PlaybookId.PullbackMa20    => "pullback-ma20",
        PlaybookId.ReversalBounce  => "reversal-bounce",
        PlaybookId.Legacy          => "legacy",
        _                          => "unclassified",
    };

    public static PlaybookId FromStringId(string? id) => id switch
    {
        "breakout-darvas" => PlaybookId.BreakoutDarvas,
        "pullback-ma20"   => PlaybookId.PullbackMa20,
        "reversal-bounce" => PlaybookId.ReversalBounce,
        "legacy"          => PlaybookId.Legacy,
        _                 => PlaybookId.Unclassified,
    };
}
