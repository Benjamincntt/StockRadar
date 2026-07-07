using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Infrastructure.Ml;

internal sealed class FileOpportunityRankerModelStore(
    IHostEnvironment env,
    IOptions<OpportunityRankerOptions> options,
    ILogger<FileOpportunityRankerModelStore> logger) : IOpportunityRankerModelStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<OpportunityRankerModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolvePath();
        if (!File.Exists(path))
        {
            logger.LogInformation("OpportunityRanker: chưa có model tại {Path} — dùng fallback heuristic.", path);
            return OpportunityRankerModel.Untrained();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var model = await JsonSerializer.DeserializeAsync<OpportunityRankerModel>(stream, JsonOptions, cancellationToken);
            return model ?? OpportunityRankerModel.Untrained();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpportunityRanker: đọc model thất bại — fallback heuristic.");
            return OpportunityRankerModel.Untrained();
        }
    }

    public async Task SaveAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, model, JsonOptions, cancellationToken);
        logger.LogInformation(
            "OpportunityRanker: đã lưu model {Samples} mẫu, accuracy {Acc:0.#}% → {Path}.",
            model.TrainingSamples,
            model.TrainingAccuracy,
            path);
    }

    private string ResolvePath()
    {
        var rel = options.Value.ModelPath.Trim();
        return Path.IsPathRooted(rel)
            ? rel
            : Path.Combine(env.ContentRootPath, rel);
    }
}

/// <summary>Nạp model ranker khi API khởi động.</summary>
internal sealed class OpportunityRankerBootstrap(
    IOpportunityRanker ranker,
    ILogger<OpportunityRankerBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ranker.ReloadModelAsync(cancellationToken);
        var snap = ranker.GetModelSnapshot();
        if (snap.IsTrained)
        {
            logger.LogInformation(
                "OpportunityRanker active: {Samples} mẫu, accuracy {Acc:0.#}%.",
                snap.TrainingSamples,
                snap.TrainingAccuracy);
        }
        else
        {
            logger.LogInformation("OpportunityRanker: chưa train — sort dùng PredictedHitPercent fallback.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
