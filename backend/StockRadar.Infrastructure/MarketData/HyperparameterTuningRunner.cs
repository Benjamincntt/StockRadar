using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class HyperparameterTuningRunner(
    ITelegramNotifier telegram,
    IOptions<HyperparameterTuningOptions> tuningOptions,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<MarketJobsOptions> marketJobsOptions,
    ILogger<HyperparameterTuningRunner> logger) : IHyperparameterTuningService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task RunWeeklyAsync(CancellationToken cancellationToken = default)
    {
        var cfg = tuningOptions.Value;
        if (!cfg.Enabled)
        {
            logger.LogDebug("HyperparameterTuning tắt — bỏ qua.");
            return;
        }

        if (!File.Exists(cfg.PythonPath))
        {
            logger.LogWarning(
                "HPO: không tìm thấy Python {Path}. Chạy scripts/setup-tune-venv.sh trên server.",
                cfg.PythonPath);
            await telegram.SendAsync(
                "⚠️ [StockRadar HPO] Bỏ qua tuning — chưa có .venv-tune. Chạy setup-tune-venv.sh trên server.",
                cancellationToken);
            return;
        }

        if (!File.Exists(cfg.ScriptPath))
        {
            logger.LogWarning("HPO: không tìm thấy script {Path}.", cfg.ScriptPath);
            return;
        }

        var outputDir = Path.GetDirectoryName(cfg.OutputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        logger.LogInformation(
            "HPO weekly: {Trials} trials, {Days} ngày → {Output}",
            cfg.Trials,
            cfg.Days,
            cfg.OutputPath);

        var args =
            $"\"{cfg.ScriptPath}\" --trials {cfg.Trials} --days {cfg.Days} " +
            $"--timeout {cfg.TimeoutPerTrialSeconds} --output \"{cfg.OutputPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = cfg.PythonPath,
            Arguments = args,
            WorkingDirectory = cfg.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var timeout = TimeSpan.FromMinutes(Math.Max(5, cfg.ProcessTimeoutMinutes));

        var completed = await Task.Run(
            () => process.WaitForExit((int)timeout.TotalMilliseconds),
            cancellationToken);

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }

            logger.LogError("HPO process timeout sau {Min} phút.", cfg.ProcessTimeoutMinutes);
            await telegram.SendAsync(
                $"⚠️ [StockRadar HPO] Tuning timeout sau {cfg.ProcessTimeoutMinutes} phút.",
                cancellationToken);
            return;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning("HPO exit {Code}. stderr: {Err}", process.ExitCode, stderr);
            await telegram.SendAsync(
                $"⚠️ [StockRadar HPO] Script lỗi (exit {process.ExitCode}). Xem log API.",
                cancellationToken);
            return;
        }

        if (!File.Exists(cfg.OutputPath))
        {
            logger.LogWarning("HPO xong nhưng thiếu file {Path}. stdout: {Out}", cfg.OutputPath, stdout);
            return;
        }

        await using var stream = File.OpenRead(cfg.OutputPath);
        var result = await JsonSerializer.DeserializeAsync<WeeklyTuningJson>(stream, JsonOptions, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("HPO: không parse được JSON.");
            return;
        }

        var message = FormatTelegramMessage(result);
        await telegram.SendAsync(message, cancellationToken);
        logger.LogInformation("HPO weekly xong — fitness {Fitness}.", result.BestFitness);
    }

    private string FormatTelegramMessage(WeeklyTuningJson result)
    {
        var currentPass = smartMoneyOptions.Value.MinPassScore;
        var currentMax = marketJobsOptions.Value.DailyAnalysis.MaxResults;

        if (result.BestFitness is null || result.BestParams is null)
        {
            return "⚠️ [StockRadar HPO] Không trial nào thành công tuần này. Giữ nguyên cấu hình prod.";
        }

        var pass = result.BestParams.MinPassScore;
        var maxRes = result.BestParams.MaxResults;
        var metrics = result.BestMetrics;
        var hitPct = metrics?.HitRateTopK is decimal h ? (h * 100m).ToString("0.#", CultureInfo.InvariantCulture) : "—";
        var trades = metrics?.TotalTrades?.ToString(CultureInfo.InvariantCulture) ?? "—";
        var fitness = result.BestFitness.Value.ToString("0.##", CultureInfo.InvariantCulture);

        var passNote = pass == currentPass ? "(giữ)" : $"(đang: {currentPass})";
        var maxNote = maxRes == currentMax ? "(giữ)" : $"(đang: {currentMax})";

        return
            "🤖 [StockRadar HPO] Đề xuất tham số tuần mới\n\n" +
            $"Dữ liệu: {result.Days} ngày | Trials: {result.Trials}\n" +
            $"Best fitness: {fitness}\n" +
            $"Hit T+2.5 (top): {hitPct}% ({trades} lệnh)\n\n" +
            "⚙️ Đề xuất AI (Tầng 2):\n" +
            $"MinPassScore: {pass} {passNote}\n" +
            $"MaxResults: {maxRes} {maxNote}\n\n" +
            "Không auto-apply — cập nhật appsettings thủ công nếu đồng ý.";
    }

    private sealed class WeeklyTuningJson
    {
        public int Trials { get; init; }
        public int Days { get; init; }
        public decimal? BestFitness { get; init; }
        public TuningParamsJson? BestParams { get; init; }
        public TuningMetricsJson? BestMetrics { get; init; }
    }

    private sealed class TuningParamsJson
    {
        [JsonPropertyName("min_pass_score")]
        public int MinPassScore { get; init; }

        [JsonPropertyName("max_results")]
        public int MaxResults { get; init; }
    }

    private sealed class TuningMetricsJson
    {
        [JsonPropertyName("hitRateTopK")]
        public decimal? HitRateTopK { get; init; }

        [JsonPropertyName("totalTrades")]
        public int? TotalTrades { get; init; }
    }
}
