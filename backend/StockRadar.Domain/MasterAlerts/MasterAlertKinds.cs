namespace StockRadar.Domain.MasterAlerts;

public static class MasterAlertKinds
{
    public const string SourceTag = "Master";

    public const string BuyPoint1 = "MuaDiem1";
    public const string BuyPoint2 = "MuaDiem2";
    public const string CutLoss1 = "CatLoDiem1";
    public const string CutAll = "CatHet";

    public const string Opportunity = "TopCoHoi";

    public static string Label(string kind) => kind switch
    {
        BuyPoint1 => "Mua điểm 1",
        BuyPoint2 => "Mua điểm 2",
        CutLoss1 => "Cắt lỗ điểm 1",
        CutAll => "Cắt hết",
        Opportunity => "Top cơ hội",
        _ => kind
    };

    public static bool IsBuyKind(string kind) =>
        kind is BuyPoint1 or BuyPoint2;

    public static bool IsSellKind(string kind) =>
        kind is CutLoss1 or CutAll;
}
