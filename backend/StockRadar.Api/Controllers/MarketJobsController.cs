using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Jobs;
using StockRadar.Application.Options;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/market/jobs")]
[Produces("application/json")]
[Tags("Market Jobs")]
public sealed class MarketJobsController(
    IHistoryBackfillService history,
    IDailySessionSyncService session,
    IDailyAnalysisService analysis,
    IIntradayScannerService scanner,
    IOpportunityIntradayMonitorService monitor,
    IVipTelegramAlertTestService vipTelegramTest,
    IDailyCriterionScoringService criterionScoring,
    IUniverseRescreenService universeRescreen,
    IKbsMarketSyncService kbsSync,
    IOpportunityPerformanceService performance,
    IJobStatusService jobStatus,
    ISectorWaveRegimeBackfillService sectorWaveRegimeBackfill,
    IOptions<MarketDataOptions> marketOptions) : ControllerBase
{
    /// <summary>Danh sách toàn bộ pipeline job + lần chạy cuối (xếp theo tần suất) — cho màn hình Jobs.</summary>
    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyList<JobStatusDto>>> GetJobs(CancellationToken cancellationToken)
        => Ok(await jobStatus.GetAllAsync(cancellationToken));

    [HttpGet("history/status")]
    public ActionResult<HistoryBackfillStatusDto> HistoryStatus() => Ok(history.GetStatus());

    [HttpPost("history")]
    public async Task<ActionResult<HistoryBackfillResultDto>> RunHistory(
        [FromBody] HistoryBackfillRequest? request,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        try
        {
            var req = request ?? new HistoryBackfillRequest(Mode: "fast");
            if (history.GetStatus().IsRunning)
                return Conflict(new { message = "Job 1 đang chạy." });
            var result = await jobStatus.TrackAsync(
                JobCatalog.HistoryBackfill, "manual",
                c => history.RunAsync(req with { Mode = "fast" }, c),
                r => $"{r.SymbolsInUniverse}/{r.SymbolsTotal} mã · {r.BarsWritten} nến",
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Job 1 chế độ đêm — delay lớn hơn, giảm tải API KBS.</summary>
    [HttpPost("history/night")]
    public async Task<ActionResult<HistoryBackfillResultDto>> RunHistoryNight(
        [FromBody] HistoryBackfillRequest? request,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        if (history.GetStatus().IsRunning)
            return Conflict(new { message = "Job 1 đang chạy." });
        var req = request ?? new HistoryBackfillRequest();
        return Ok(await history.RunAsync(req with { Mode = "night" }, cancellationToken));
    }

    [HttpPost("session")]
    public async Task<ActionResult<DailySessionSyncResultDto>> RunSession(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var result = await jobStatus.TrackAsync(
            JobCatalog.DailySessionSync, "manual",
            c => session.RunAsync(c),
            r => $"{r.SymbolsSynced} mã · {r.DarvasBreakoutAlerts} Darvas",
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Loại mã rác khỏi universe (giá / thanh khoản) — chạy cuối Job 1 hoặc bảo trì thủ công.</summary>
    [HttpPost("universe-rescreen")]
    public async Task<ActionResult<UniverseRescreenResultDto>> RunUniverseRescreen(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await universeRescreen.RunAsync(cancellationToken));
    }

    [HttpPost("analysis")]
    public async Task<ActionResult<DailyAnalysisResultDto>> RunAnalysis(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var result = await jobStatus.TrackAsync(
            JobCatalog.DailyAnalysis, "manual",
            c => analysis.RunAsync(c),
            r => $"{r.StocksScored} mã · {r.OpportunitiesSaved} cơ hội",
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Job 2 (append phiên T) + phân tích watchlist (không phải Job 3 intraday).</summary>
    [HttpPost("daily")]
    public async Task<ActionResult<object>> RunDailyPipeline(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        var sessionResult = await session.RunAsync(cancellationToken);
        var analysisResult = await analysis.RunAsync(cancellationToken);
        return Ok(new { session = sessionResult, analysis = analysisResult });
    }

    /// <summary>Chấm ngược tiêu chí N ngày quá khứ để lấp đầy rolling 7/30 ngày ngay lập tức.</summary>
    [HttpPost("criteria-backfill")]
    public async Task<ActionResult<object>> RunCriteriaBackfill(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var scoredDates = await criterionScoring.RunBackfillAsync(days, cancellationToken);
        return Ok(new { requestedDays = days, scoredDates });
    }

    /// <summary>
    /// Backfill một lần trạng thái Sóng ngành (spec 007) từ lịch sử OHLCV, point-in-time
    /// (không nhìn thấy dữ liệu tương lai). KHÔNG đụng Buy Score/Top/DailyOpportunities.
    /// </summary>
    [HttpPost("sector-wave-backfill")]
    public async Task<ActionResult<SectorWaveRegimeBackfillResultDto>> RunSectorWaveBackfill(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        [FromQuery] DateOnly fromDate,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await sectorWaveRegimeBackfill.RunAsync(fromDate, cancellationToken));
    }

    [HttpPost("intraday-scan")]
    public async Task<ActionResult<object>> RunIntradayScan(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var count = await jobStatus.TrackAsync(
            JobCatalog.IntradayScanner, "manual",
            c => scanner.ScanAsync(c),
            n => $"{n} tín hiệu",
            cancellationToken);
        return Ok(new { matchCount = count });
    }

    /// <summary>Job 3: monitor intraday 60s (thường chạy qua hosted service).</summary>
    [HttpPost("opportunity-monitor")]
    public async Task<ActionResult<object>> RunOpportunityMonitor(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var alerts = await jobStatus.TrackAsync(
            JobCatalog.OpportunityMonitor, "manual",
            c => monitor.RunAsync(c),
            n => $"{n} cảnh báo",
            cancellationToken);
        return Ok(new { alertsSent = alerts });
    }

    /// <summary>Chạy 1 vòng đồng bộ giá KBS thủ công (thường chạy tự động ~60s).</summary>
    [HttpPost("kbs-sync")]
    public async Task<ActionResult<object>> RunKbsSync(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        await jobStatus.TrackAsync<object?>(
            JobCatalog.KbsMarketSync, "manual",
            async c => { await kbsSync.RunAsync(c); return null; },
            cancellationToken: cancellationToken);
        return Ok(new { ok = true });
    }

    /// <summary>Review hiệu quả Top cơ hội (phần review; retrain ML/HPO vẫn theo lịch tuần).</summary>
    [HttpPost("weekly-review")]
    public async Task<ActionResult<object>> RunWeeklyReview(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var review = await jobStatus.TrackAsync(
            JobCatalog.WeeklyOpportunityReview, "manual",
            c => performance.RunWeeklyReviewAsync(cancellationToken: c),
            r => r is null ? "Không có dữ liệu review" : "Đã cập nhật review tuần",
            cancellationToken);
        return Ok(new { ok = true, review });
    }

    /// <summary>Gửi 4 tin Telegram mẫu VIP (fake GAS) — test format, không ghi DB.</summary>
    [HttpPost("telegram/vip-test")]
    public async Task<ActionResult<VipTelegramTestResultDto>> SendVipTelegramTest(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        var result = await vipTelegramTest.SendSampleAlertsAsync(cancellationToken);
        if (result.MessagesSent == 0 && result.Error is not null)
            return BadRequest(result);

        return Ok(result);
    }

    private bool IsAuthorized(string? syncKey) =>
        User.Identity?.IsAuthenticated == true
        || (!string.IsNullOrWhiteSpace(marketOptions.Value.SyncApiKey)
            && syncKey == marketOptions.Value.SyncApiKey);
}
