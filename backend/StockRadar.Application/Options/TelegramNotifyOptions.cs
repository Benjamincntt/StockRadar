namespace StockRadar.Application.Options;

public sealed class TelegramNotifyOptions
{
    public const string SectionName = "TelegramNotify";

    public bool Enabled { get; set; }

    public string BotToken { get; set; } = "";

    public string ChatId { get; set; } = "";

    public int TimeoutGiay { get; set; } = 10;
}
