using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

/// <summary>Tin HPO đi song song Telegram và kênh đẩy "Hệ thống" (FR-001a, T028).</summary>
public sealed class GuiThongBaoHeThongTests : IDisposable
{
    private const string TinBoQua =
        "⚠️ [StockRadar HPO] Bỏ qua tuning — chưa có .venv-tune. Chạy setup-tune-venv.sh trên server.";

    private readonly string thuMuc = Path.Combine(Path.GetTempPath(), "stockradar-hpo-" + Guid.NewGuid().ToString("N"));
    private readonly BoThuGuiThongBao boThu = new();

    public GuiThongBaoHeThongTests() => Directory.CreateDirectory(thuMuc);

    public void Dispose()
    {
        try
        {
            Directory.Delete(thuMuc, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm còn khoá file của process con: bỏ qua, hệ điều hành dọn sau.
        }
    }

    [Fact]
    public async Task BoQuaKhiThieuPythonVanGuiCaHaiKenh()
    {
        var cauHinh = CauHinh(Path.Combine(thuMuc, "khong-co-python"), Path.Combine(thuMuc, "tune-optuna.py"));
        File.WriteAllText(cauHinh.ScriptPath, "# script gia");

        await TaoRunner(cauHinh).RunWeeklyAsync();

        var telegram = Assert.Single(boThu.Telegram.Tin);
        Assert.Equal(TinBoQua, telegram.NoiDung);
        Assert.Null(telegram.DinhDang);

        var day = Assert.Single(boThu.Day.Tin).NoiDung;
        Assert.Equal("HPO · bỏ qua", day.TieuDe);
        Assert.Equal(TinBoQua, day.NoiDung);
        Assert.Equal("he-thong", day.Kenh);
        Assert.Null(day.Symbol);
        Assert.Null(day.LoaiV3);
        KiemTraTinHeThong(day);
    }

    [Fact]
    public async Task LoiKhiScriptThoatKhacKhongVanGuiCaHaiKenh()
    {
        var (python, script) = TaoPythonGia("Write-Error 'loi gia'; exit 1", "exit 1");
        var cauHinh = CauHinh(python, script);

        await TaoRunner(cauHinh).RunWeeklyAsync();

        var telegram = Assert.Single(boThu.Telegram.Tin);
        Assert.StartsWith("⚠️ [StockRadar HPO] Script lỗi (exit", telegram.NoiDung);
        Assert.EndsWith("Xem log API.", telegram.NoiDung);
        Assert.Null(telegram.DinhDang);

        var day = Assert.Single(boThu.Day.Tin).NoiDung;
        Assert.Equal("HPO · lỗi", day.TieuDe);
        Assert.Equal(telegram.NoiDung, day.NoiDung);
        Assert.Equal("he-thong", day.Kenh);
        Assert.Null(day.Symbol);
        KiemTraTinHeThong(day);
    }

    [Fact]
    public async Task XongThiTelegramGiuNguyenChuoiCuVaDayLenKenhHeThong()
    {
        var (python, script) = TaoPythonGia("exit 0", "exit 0");
        var cauHinh = CauHinh(python, script);
        await File.WriteAllTextAsync(cauHinh.OutputPath, """
            {"trials":2,"days":5,"bestFitness":1.25,
             "bestParams":{"max_results":30},
             "bestMetrics":{"hitRateTopK":0.5,"totalTrades":40}}
            """);

        await TaoRunner(cauHinh).RunWeeklyAsync();

        const string chuoiCu =
            "🤖 [StockRadar HPO] Đề xuất tham số tuần mới\n\n" +
            "Dữ liệu: 5 ngày | Trials: 2\n" +
            "Best fitness: 1.25\n" +
            "Hit T+2.5 (top): 50% (40 lệnh)\n\n" +
            "⚙️ Đề xuất AI (Tầng 2):\n" +
            "MaxResults: 30 (giữ)\n\n" +
            "Không auto-apply — cập nhật appsettings thủ công nếu đồng ý.";

        var telegram = Assert.Single(boThu.Telegram.Tin);
        Assert.Equal(chuoiCu, telegram.NoiDung);
        Assert.Null(telegram.DinhDang);

        var day = Assert.Single(boThu.Day.Tin).NoiDung;
        Assert.Equal("HPO · xong", day.TieuDe);
        Assert.Equal(chuoiCu, day.NoiDung);
        Assert.Equal("he-thong", day.Kenh);
        Assert.Null(day.Symbol);
        KiemTraTinHeThong(day);
    }

    [Fact]
    public async Task KenhDayNemThiRunnerVanChayHetVaLoiKhongLanRaNgoai()
    {
        boThu.Day.KhiGui = _ => throw new InvalidOperationException("kenh-day-gia-nem");
        var (python, script) = TaoPythonGia("exit 0", "exit 0");
        var cauHinh = CauHinh(python, script);
        await File.WriteAllTextAsync(cauHinh.OutputPath, """{"trials":1,"days":5,"bestFitness":0.5}""");

        await TaoRunner(cauHinh).RunWeeklyAsync();

        Assert.Single(boThu.Telegram.Tin);
        Assert.Single(boThu.Day.Tin);
        Assert.True(boThu.Log.Dem(LogLevel.Warning) >= 1);
    }

    [Fact]
    public void BonTieuDeHpoDiQuaGuiThongBaoChuKhongGoiThangTelegram()
    {
        var goc = TimThuMucGoc();
        var noiDung = File.ReadAllText(Path.Combine(goc, "backend", "StockRadar.Infrastructure", "MarketData",
            "HyperparameterTuningRunner.cs"));

        Assert.Equal(4, noiDung.Split("guiTin.GuiHeThongAsync(").Length - 1);
        foreach (var tieuDe in new[] { "HPO · bỏ qua", "HPO · quá giờ", "HPO · lỗi", "HPO · xong" })
            Assert.Contains($"\"{tieuDe}\"", noiDung, StringComparison.Ordinal);
        Assert.DoesNotContain("telegram.SendAsync", noiDung, StringComparison.Ordinal);
        Assert.Contains("private string FormatTelegramMessage", noiDung, StringComparison.Ordinal);
    }

    private HyperparameterTuningRunner TaoRunner(HyperparameterTuningOptions cauHinh) =>
        new(boThu.Tao(), Options.Create(cauHinh), Options.Create(new MarketJobsOptions()),
            NullLogger<HyperparameterTuningRunner>.Instance);

    private HyperparameterTuningOptions CauHinh(string python, string script) => new()
    {
        Enabled = true,
        PythonPath = python,
        ScriptPath = script,
        OutputPath = Path.Combine(thuMuc, "weekly-tuning-result.json"),
        RepoRoot = thuMuc,
        Trials = 2,
        Days = 5,
        TimeoutPerTrialSeconds = 1,
    };

    private void KiemTraTinHeThong(DayTinNhan day)
    {
        var tinSdk = KenhDayTinFcm.TaoTinNhan("thiet-bi-gia-12345", day);
        Assert.Equal(Priority.Normal, tinSdk.Android.Priority);
        Assert.Equal("he-thong", tinSdk.Android.Notification.ChannelId);
        Assert.Equal(TimeSpan.FromHours(12), tinSdk.Android.TimeToLive);
        Assert.False(tinSdk.Data.ContainsKey("symbol"));
    }

    /// <summary>Script "python" giả chỉ thoát bằng mã mong muốn, để không cần Python trên máy test.</summary>
    private (string Python, string Script) TaoPythonGia(string noiDungPs1, string noiDungSh)
    {
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(thuMuc, "tune-gia.ps1");
            File.WriteAllText(script, noiDungPs1);
            var heThong = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return (Path.Combine(heThong, "WindowsPowerShell", "v1.0", "powershell.exe"), script);
        }

        var sh = Path.Combine(thuMuc, "tune-gia.sh");
        File.WriteAllText(sh, "#!/bin/sh\n" + noiDungSh + "\n");
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return ("/bin/sh", sh);
    }

    private static string TimThuMucGoc()
    {
        for (var thuMuc = new DirectoryInfo(AppContext.BaseDirectory); thuMuc is not null; thuMuc = thuMuc.Parent)
            if (File.Exists(Path.Combine(thuMuc.FullName, "StockRadar.slnx")))
                return thuMuc.Parent!.FullName;

        throw new InvalidOperationException("Không tìm thấy thư mục gốc chứa backend/StockRadar.slnx.");
    }
}
