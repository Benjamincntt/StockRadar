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
        var path = ResolveActivePath();
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
        var versionPath = await WriteVersionFileAsync(model, cancellationToken);
        await CopyToActiveAsync(versionPath, cancellationToken);
        PruneOldVersions();

        logger.LogInformation(
            "OpportunityRanker: đã lưu model {Samples} mẫu, accuracy {Acc:0.#}% → {Path}.",
            model.TrainingSamples,
            model.TrainingAccuracy,
            versionPath);
    }

    public async Task SaveVersionOnlyAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default)
    {
        var versionPath = await WriteVersionFileAsync(model, cancellationToken);
        PruneOldVersions();
        logger.LogInformation(
            "OpportunityRanker: lưu version (chưa promote) {Samples} mẫu → {Path}.",
            model.TrainingSamples,
            versionPath);
    }

    public async Task<IReadOnlyList<OpportunityRankerModelVersionInfo>> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var active = await LoadAsync(cancellationToken);
        var dir = ResolveVersionsDirectory();
        if (!Directory.Exists(dir))
            return [];

        var versions = Directory.GetFiles(dir, "opportunity-ranker-*.json")
            .Select(path =>
            {
                var fileName = Path.GetFileName(path);
                var meta = TryReadMeta(path);
                var isActive = meta is not null
                    && active.IsTrained
                    && meta.TrainedAtUtc == active.TrainedAtUtc
                    && meta.TrainingSamples == active.TrainingSamples
                    && Math.Abs(meta.TrainingAccuracy - active.TrainingAccuracy) < 0.05m;
                return new OpportunityRankerModelVersionInfo(
                    fileName,
                    meta?.TrainedAtUtc,
                    meta?.TrainingSamples ?? 0,
                    meta?.TrainingAccuracy ?? 0,
                    isActive);
            })
            .OrderByDescending(v => v.TrainedAtUtc ?? DateTime.MinValue)
            .ToList();

        return versions;
    }

    public async Task<bool> RevertToVersionAsync(string versionFileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionFileName)
            || versionFileName.Contains("..", StringComparison.Ordinal)
            || versionFileName.Contains('/', StringComparison.Ordinal)
            || versionFileName.Contains('\\', StringComparison.Ordinal))
            return false;

        var versionPath = Path.Combine(ResolveVersionsDirectory(), versionFileName);
        if (!File.Exists(versionPath))
            return false;

        await CopyToActiveAsync(versionPath, cancellationToken);
        logger.LogInformation("OpportunityRanker: revert active model → {File}.", versionFileName);
        return true;
    }

    internal async Task<string> WriteVersionFileAsync(
        OpportunityRankerModel model,
        CancellationToken cancellationToken)
    {
        var dir = ResolveVersionsDirectory();
        Directory.CreateDirectory(dir);

        var stamp = (model.TrainedAtUtc ?? DateTime.UtcNow).ToString("yyyyMMddHHmmss");
        var versionPath = Path.Combine(dir, $"opportunity-ranker-{stamp}.json");
        await using (var stream = File.Create(versionPath))
            await JsonSerializer.SerializeAsync(stream, model, JsonOptions, cancellationToken);

        return versionPath;
    }

    internal async Task CopyToActiveAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var activePath = ResolveActivePath();
        var dir = Path.GetDirectoryName(activePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var source = File.OpenRead(sourcePath);
        await using var dest = File.Create(activePath);
        await source.CopyToAsync(dest, cancellationToken);
    }

    private void PruneOldVersions()
    {
        var keep = Math.Max(1, options.Value.KeepModelVersions);
        var dir = ResolveVersionsDirectory();
        if (!Directory.Exists(dir))
            return;

        var files = Directory.GetFiles(dir, "opportunity-ranker-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(keep)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OpportunityRanker: không xóa được version cũ {File}.", file);
            }
        }
    }

    private static OpportunityRankerModel? TryReadMeta(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OpportunityRankerModel>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private string ResolveActivePath()
    {
        var rel = options.Value.ModelPath.Trim();
        return Path.IsPathRooted(rel)
            ? rel
            : Path.Combine(env.ContentRootPath, rel);
    }

    private string ResolveVersionsDirectory()
    {
        var rel = options.Value.ModelVersionsDirectory.Trim();
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
