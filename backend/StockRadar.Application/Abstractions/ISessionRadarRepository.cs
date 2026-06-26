using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface ISessionRadarRepository
{
    Task ReplaceSessionHitsAsync(
        DateOnly sessionDate,
        string exchange,
        IReadOnlyList<SessionRadarHitRecord> hits,
        DateTime scannedAt,
        CancellationToken cancellationToken = default);

    Task<RadarLiveSnapshotDto> GetLiveSnapshotAsync(
        RadarLiveQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record SessionRadarHitRecord(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal ChangePercent,
    long SessionVolume,
    decimal VolumeRatio,
    decimal RelativeStrength,
    IReadOnlyList<string> Signals);
