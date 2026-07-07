using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/ml")]
[Tags("ML")]
public sealed class MlController(
    IOpportunityRankingDatasetService dataset,
    IOpportunityRankerTrainingService training,
    IOpportunityRanker ranker,
    IOpportunityRankerModelStore modelStore,
    ISetupTrackBackfillService setupBackfill,
    ITuneEvaluateService tuneEvaluate,
    IOptions<MarketDataOptions> marketOptions,
    IOptions<OpportunityRankerOptions> rankerOptions) : ControllerBase
{
    /// <summary>Dataset T+2.5 ranking — features T0 + label đo thực tế.</summary>
    [HttpGet("dataset/t25-ranking")]
    [ProducesResponseType(typeof(OpportunityRankingDatasetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataset(
        [FromQuery] int days = 180,
        [FromQuery] string format = "json",
        [FromHeader(Name = "X-Sync-Key")] string? syncKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        var lookback = days > 0 ? days : rankerOptions.Value.DefaultDatasetDays;
        var result = await dataset.BuildAsync(lookback, cancellationToken);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = dataset.ToCsv(result);
            return Content(csv, "text/csv; charset=utf-8");
        }

        return Ok(result);
    }

    /// <summary>Train logistic regression từ SetupTracks và lưu model JSON.</summary>
    [HttpPost("train/t25-ranking")]
    [ProducesResponseType(typeof(OpportunityRankerTrainingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityRankerTrainingResultDto>> Train(
        [FromQuery] int days = 180,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        return Ok(await training.TrainAndSaveAsync(
            days > 0 ? days : rankerOptions.Value.DefaultDatasetDays,
            cancellationToken));
    }

    /// <summary>Backfill SetupTracks từ lịch sử DailyOpportunities + đo T+2.5.</summary>
    [HttpPost("backfill/setup-tracks")]
    [ProducesResponseType(typeof(SetupTrackBackfillResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupTrackBackfillResultDto>> BackfillSetupTracks(
        [FromQuery] int days = 180,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        return Ok(await setupBackfill.BackfillFromDailyOpportunitiesAsync(
            days > 0 ? days : rankerOptions.Value.DefaultDatasetDays,
            cancellationToken));
    }

    /// <summary>Headless backtest replay — trả fitness cho Optuna (không ghi DB).</summary>
    [HttpPost("tune/evaluate")]
    [ProducesResponseType(typeof(TuneEvaluateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TuneEvaluateResponse>> EvaluateTuneParams(
        [FromBody] TuneEvaluateRequest request,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        return Ok(await tuneEvaluate.EvaluateAsync(request, cancellationToken));
    }

    [HttpGet("ranker/status")]
    [ProducesResponseType(typeof(OpportunityRankerStatusDto), StatusCodes.Status200OK)]
    public ActionResult<OpportunityRankerStatusDto> GetRankerStatus()
    {
        var snap = ranker.GetModelSnapshot();
        var weights = snap.IsTrained
            ? snap.Weights.Select(w => (decimal)Math.Round(w, 4)).ToList()
            : null;

        return Ok(new OpportunityRankerStatusDto(
            rankerOptions.Value.Enabled,
            ranker.IsModelActive,
            snap.TrainingSamples,
            snap.IsTrained ? snap.TrainingAccuracy : null,
            snap.TrainedAtUtc,
            snap.FeatureNames,
            weights,
            rankerOptions.Value.AutoRetrainEnabled));
    }

    [HttpGet("ranker/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<OpportunityRankerModelVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpportunityRankerModelVersionDto>>> ListVersions(
        CancellationToken cancellationToken)
    {
        var versions = await modelStore.ListVersionsAsync(cancellationToken);
        return Ok(versions.Select(v => new OpportunityRankerModelVersionDto(
            v.FileName,
            v.TrainedAtUtc,
            v.TrainingSamples,
            v.TrainingAccuracy,
            v.IsActive)).ToList());
    }

    [HttpPost("ranker/revert")]
    [ProducesResponseType(typeof(OpportunityRankerTrainingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityRankerTrainingResultDto>> RevertModel(
        [FromQuery] string version,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(syncKey))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(new OpportunityRankerTrainingResultDto(
                false, 0, 0, 0, null, null, "Thiếu query ?version=opportunity-ranker-....json"));

        var ok = await modelStore.RevertToVersionAsync(version, cancellationToken);
        if (!ok)
        {
            return NotFound(new OpportunityRankerTrainingResultDto(
                false, 0, 0, 0, null, null, $"Không tìm thấy version '{version}'."));
        }

        await ranker.ReloadModelAsync(cancellationToken);
        var snap = ranker.GetModelSnapshot();
        return Ok(new OpportunityRankerTrainingResultDto(
            true,
            snap.TrainingSamples,
            snap.TrainingAccuracy,
            0,
            snap.TrainedAtUtc,
            rankerOptions.Value.ModelPath,
            $"Đã revert active model → {version}."));
    }

    private bool IsAuthorized(string? syncKey) =>
        !string.IsNullOrWhiteSpace(marketOptions.Value.SyncApiKey)
        && string.Equals(syncKey, marketOptions.Value.SyncApiKey, StringComparison.Ordinal);
}
