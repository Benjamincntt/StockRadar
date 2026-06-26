using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
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
    IOptions<MarketDataOptions> marketOptions) : ControllerBase
{
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
            return Ok(await history.RunAsync(req with { Mode = "fast" }, cancellationToken));
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
        return Ok(await session.RunAsync(cancellationToken));
    }

    [HttpPost("analysis")]
    public async Task<ActionResult<DailyAnalysisResultDto>> RunAnalysis(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await analysis.RunAsync(cancellationToken));
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

    [HttpPost("intraday-scan")]
    public async Task<ActionResult<object>> RunIntradayScan(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var count = await scanner.ScanAsync(cancellationToken);
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
        var alerts = await monitor.RunAsync(cancellationToken);
        return Ok(new { alertsSent = alerts });
    }

    private bool IsAuthorized(string? syncKey) =>
        !string.IsNullOrWhiteSpace(marketOptions.Value.SyncApiKey)
        && syncKey == marketOptions.Value.SyncApiKey;
}
