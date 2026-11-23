using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Jobs;
using StockRadar.Application.Options;
using StockRadar.Api.Security;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/market/jobs")]
[Produces("application/json")]
[Tags("Market Jobs")]
public sealed class MarketJobsController(
    IHistoryBackfillService history,
    IDailySessionSyncService session,
    IUniverseRescreenService universeRescreen,
    IKbsMarketSyncService kbsSync,
    IJobStatusService jobStatus,
    IV3TruocPhienService v3TruocPhien,
    IV3TrongPhienService v3TrongPhien,
    IV3AtcService v3Atc,
    IV3SauPhienService v3SauPhien,
    IV3NapChiSoBackfillService v3NapChiSo,
    IMuaTetService muaTet,
    IMuaTetTheoDoiService muaTetTheoDoi,
    ICurrentUserService currentUser,
    IOptions<MarketDataOptions> marketOptions) : ControllerBase
{
    /// <summary>Danh sách toàn bộ pipeline job + lần chạy cuối (xếp theo tần suất) — cho màn hình Jobs.</summary>
    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyList<JobStatusDto>>> GetJobs(CancellationToken cancellationToken)
        => Ok(await jobStatus.GetAllAsync(cancellationToken));

    [HttpGet("history/status")]
    public ActionResult<HistoryBackfillStatusDto> HistoryStatus() => Ok(history.GetStatus());

    [HttpPost("history")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
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
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
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
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<DailySessionSyncResultDto>> RunSession(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        var result = await jobStatus.TrackAsync(
            JobCatalog.DailySessionSync, "manual",
            c => session.RunAsync(c),
            r => $"{r.SymbolsSynced} mã",
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Loại mã rác khỏi universe (giá / thanh khoản) — chạy cuối Job 1 hoặc bảo trì thủ công.</summary>
    [HttpPost("universe-rescreen")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<UniverseRescreenResultDto>> RunUniverseRescreen(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await universeRescreen.RunAsync(cancellationToken));
    }

    /// <summary>Chạy 1 vòng đồng bộ giá KBS thủ công (thường chạy tự động ~60s).</summary>
    [HttpPost("kbs-sync")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
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

    /// <summary>
    /// Trigger thủ công V3 trước phiên (08:30) — KB1/KB2 bắn tin mua ATO phiên sau (spec 012).
    /// POST /api/v1/market/jobs/v3-truoc-phien
    /// </summary>
    [HttpPost("v3-truoc-phien")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<V3ChayKetQua>> ChayV3TruocPhien(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await v3TruocPhien.ChayAsync(cancellationToken));
    }

    /// <summary>
    /// Trigger thủ công V3 trong phiên — khớp ATO + xét bán realtime.
    /// POST /api/v1/market/jobs/v3-trong-phien
    /// </summary>
    [HttpPost("v3-trong-phien")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<V3ChayKetQua>> ChayV3TrongPhien(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await v3TrongPhien.ChayAsync(cancellationToken));
    }

    /// <summary>
    /// Trigger thủ công V3 ATC (~14:25) — KB4/N1/N3 mua ATC + bán hết hạn.
    /// POST /api/v1/market/jobs/v3-atc
    /// </summary>
    [HttpPost("v3-atc")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<V3ChayKetQua>> ChayV3Atc(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await v3Atc.ChayAsync(cancellationToken));
    }

    /// <summary>
    /// Trigger thủ công V3 sau phiên (16:00) — nhịp nạp đầy đủ: lưu chỉ số hôm nay + UNI phiên sau,
    /// tính sẵn ngưỡng bán cho vị thế đang giữ.
    /// POST /api/v1/market/jobs/v3-sau-phien
    /// </summary>
    [HttpPost("v3-sau-phien")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<V3ChayKetQua>> ChayV3SauPhien(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await v3SauPhien.ChayAsync(cancellationToken));
    }

    /// <summary>
    /// Nạp MỘT LẦN khi deploy: dựng chuỗi chỉ số + UNI theo ngày từ 01/2023 (hoặc ?tuNgay=yyyy-MM-dd)
    /// vào ChiSoNgayV3 để các nhịp nhẹ 08:30/14:25 đọc được ngay. KHÔNG tạo tín hiệu hay vị thế cũ.
    /// POST /api/v1/market/jobs/v3-nap-chi-so
    /// </summary>
    [HttpPost("v3-nap-chi-so")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<V3NapChiSoKetQua>> ChayV3NapChiSo(
        [FromQuery] string? tuNgay,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        DateOnly? batDau = DateOnly.TryParse(tuNgay, out var ngay) ? ngay : null;
        return Ok(await v3NapChiSo.NapAsync(batDau, cancellationToken));
    }

    /// <summary>
    /// Kịch bản "Mùa Tết — mã khỏe" (P4-19): tính giỏ 10 mã từ nến ngày đã lưu rồi gửi MỘT tin
    /// qua kênh thông báo V3. Chỉ đọc DB — không ghi bảng, không tạo vị thế.
    /// POST /api/v1/market/jobs/mua-tet-bao-tin
    /// </summary>
    [HttpPost("mua-tet-bao-tin")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<MuaTetGioDto>> ChayMuaTetBaoTin(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await muaTet.BaoTinAsync(cancellationToken));
    }

    /// <summary>
    /// POST /api/v1/market/jobs/mua-tet-kiem-lo — kiểm giỏ ĐÃ MUA và gửi tin nếu chạm ngưỡng.
    /// Bỏ qua cache chặn lặp để owner thử; job 14:20/15:40 vẫn dùng cache.
    /// </summary>
    [HttpPost("mua-tet-kiem-lo")]
    // SECURITY: script gọi bằng X-Sync-Key; bỏ [AllowAnonymous] là 401 trước khi kiểm key, job tay chết im lặng.
    [AllowAnonymous]
    public async Task<ActionResult<MuaTetTheoDoiDto>> ChayMuaTetKiemLo(
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();
        return Ok(await muaTetTheoDoi.KiemVaBaoAsync(boQuaChanLap: true, cancellationToken));
    }

    // SECURITY: chỉ admin hoặc sync key; mở cho mọi user đăng nhập là ai đăng ký cũng chạy được Job 1 và các nhịp V3.
    // Không thêm [Authorize] cấp controller: các script trong scripts/ gọi bằng header X-Sync-Key, không có JWT.
    private bool IsAuthorized(string? syncKey) =>
        JobTriggerAuthorization.IsAllowed(
            currentUser.IsAuthenticated,
            currentUser.UserId,
            syncKey,
            marketOptions.Value.SyncApiKey);
}
