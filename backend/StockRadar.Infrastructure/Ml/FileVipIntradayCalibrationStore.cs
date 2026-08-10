using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.Ml;

internal sealed class FileVipIntradayCalibrationStore(
    IHostEnvironment env,
    IOptions<MasterAlertOptions> options,
    ILogger<FileVipIntradayCalibrationStore> logger) : IVipIntradayCalibrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<HitCalibrationProfile?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolvePath();
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            var dto = await JsonSerializer.DeserializeAsync<CalibrationFileDto>(stream, JsonOptions, cancellationToken);
            if (dto?.Buckets is null)
                return null;

            var buckets = dto.Buckets.Select(b => new HitCalibrationBucket(
                b.BucketId,
                b.PredictedMin,
                b.PredictedMax,
                b.SampleCount,
                b.GoodCount,
                b.PredictedMidPercent,
                b.ActualHitRatePercent,
                b.CalibrationFactor)).ToList();
            return new HitCalibrationProfile(buckets, dto.GlobalFactor, dto.TotalSamples);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "VipIntradayCalibration: đọc thất bại.");
            return null;
        }
    }

    public async Task SaveAsync(HitCalibrationProfile profile, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var buckets = profile.Buckets.Select(b => new BucketDto(
            b.BucketId,
            b.PredictedMin,
            b.PredictedMax,
            b.SampleCount,
            b.GoodCount,
            b.PredictedMidPercent,
            b.ActualHitRatePercent,
            b.CalibrationFactor)).ToList();

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            new CalibrationFileDto(buckets, profile.GlobalFactor, profile.TotalSamples, DateTime.UtcNow),
            JsonOptions,
            cancellationToken);
    }

    private string ResolvePath()
    {
        var rel = options.Value.IntradayCalibrationPath.Trim();
        return Path.IsPathRooted(rel) ? rel : Path.Combine(env.ContentRootPath, rel);
    }

    private sealed record BucketDto(
        string BucketId,
        int PredictedMin,
        int PredictedMax,
        int SampleCount,
        int GoodCount,
        decimal PredictedMidPercent,
        decimal ActualHitRatePercent,
        decimal CalibrationFactor);

    private sealed record CalibrationFileDto(
        IReadOnlyList<BucketDto> Buckets,
        decimal GlobalFactor,
        int TotalSamples,
        DateTime SavedAtUtc);
}
