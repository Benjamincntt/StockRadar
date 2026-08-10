using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Infrastructure.Ml;

internal sealed class FileVipIntradayRankerModelStore(
    IHostEnvironment env,
    IOptions<MasterAlertOptions> options,
    ILogger<FileVipIntradayRankerModelStore> logger) : IVipIntradayRankerModelStore
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
            logger.LogInformation("VipIntradayRanker: chưa có model tại {Path}.", path);
            return OpportunityRankerModel.Untrained();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var model = await JsonSerializer.DeserializeAsync<OpportunityRankerModel>(
                stream, JsonOptions, cancellationToken);
            return model ?? OpportunityRankerModel.Untrained();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "VipIntradayRanker: đọc model thất bại.");
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
            "VipIntradayRanker: đã lưu {Samples} mẫu, AUC {Acc:0.#}% → {Path}.",
            model.TrainingSamples,
            model.TrainingAccuracy,
            path);
    }

    private string ResolvePath()
    {
        var rel = options.Value.IntradayModelPath.Trim();
        return Path.IsPathRooted(rel) ? rel : Path.Combine(env.ContentRootPath, rel);
    }
}
