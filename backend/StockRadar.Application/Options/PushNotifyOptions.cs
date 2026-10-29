namespace StockRadar.Application.Options;

public sealed class PushNotifyOptions
{
    public const string SectionName = "Push";
    public bool Enabled { get; set; } = true;
    public int TimeoutGiay { get; set; } = 2;
    public int KhoiTaoLaiSauGiay { get; set; } = 300;
    public CauHinhFirebase Firebase { get; set; } = new();
}

public sealed class CauHinhFirebase
{
    public string CredentialPath { get; set; } = "";
}
