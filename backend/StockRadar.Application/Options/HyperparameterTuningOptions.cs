namespace StockRadar.Application.Options;

/// <summary>Optuna weekly tuning — chạy sau review T6, không auto-apply.</summary>
public sealed class HyperparameterTuningOptions
{
    public const string SectionName = "HyperparameterTuning";

    public bool Enabled { get; set; }

    /// <summary>Thư mục repo (chứa scripts/tune-optuna.py).</summary>
    public string RepoRoot { get; set; } = "/var/www/StockRadar";

    public string PythonPath { get; set; } = "/var/www/StockRadar/.venv-tune/bin/python";

    public string ScriptPath { get; set; } = "/var/www/StockRadar/scripts/tune-optuna.py";

    public string OutputPath { get; set; } = "/var/www/StockRadar/Data/weekly-tuning-result.json";

    public int Trials { get; set; } = 50;

    public int Days { get; set; } = 60;

    public int TimeoutPerTrialSeconds { get; set; } = 300;

    /// <summary>Timeout toàn bộ process Python.</summary>
    public int ProcessTimeoutMinutes { get; set; } = 45;
}

public sealed class TelegramNotifyOptions
{
    public const string SectionName = "TelegramNotify";

    public bool Enabled { get; set; }

    /// <summary>Master + Entry zone alerts trong phiên (Top cơ hội).</summary>
    public bool VipAlertsEnabled { get; set; } = true;

    public string BotToken { get; set; } = "";

    public string ChatId { get; set; } = "";
}
