using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/ml")]
[Tags("ML")]
public sealed class MlController(
    IOpportunityRankingDatasetService dataset,
    IOpportunityRankerTrainingService training,
    IOpportunityRanker ranker,
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
            weights));
    }

    private bool IsAuthorized(string? syncKey) =>
        !string.IsNullOrWhiteSpace(marketOptions.Value.SyncApiKey)
        && string.Equals(syncKey, marketOptions.Value.SyncApiKey, StringComparison.Ordinal);
}
