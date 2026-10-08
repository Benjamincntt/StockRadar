namespace StockRadar.Domain.Constants;

/// <summary>Tên sự kiện bán và lý do thoát dùng trong Pha 2 / Pha 3. Chi tiết: docs/features/v2-sell-plan-tracking/spec.md.</summary>
public static class SuKienBan
{
    public const string ChamDungLo   = "ChamDungLo";
    public const string ChamChotLoi1 = "ChamChotLoi1";
    public const string ChamChotLoi2 = "ChamChotLoi2";
    public const string KietSuc      = "KietSuc";
    public const string GayNen       = "GayNen";

    public const string LyDoDungLo   = "DungLo";
    public const string LyDoChotLoi2 = "ChotLoi2";
    public const string LyDoGayNen   = "GayNen";
}
