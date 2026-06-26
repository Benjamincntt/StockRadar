namespace StockRadar.Application.DTOs;

public enum RadarLiveDirection
{
    All,
    Up,
    Down
}

public sealed record RadarLiveItemDto(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal ChangePercent,
    long SessionVolume,
    decimal VolumeRatio,
    decimal RelativeStrength,
    IReadOnlyList<string> Signals,
    DateTime ScannedAt);

public sealed record RadarLiveSnapshotDto(
    string Exchange,
    DateOnly SessionDate,
    DateTime ScannedAt,
    int MatchCount,
    IReadOnlyList<RadarLiveItemDto> Items);

public sealed class RadarLiveQuery
{
    public long MinSessionVolume { get; set; } = 1_000_000;
    public decimal MinAbsChangePercent { get; set; } = 3m;
    public RadarLiveDirection Direction { get; set; } = RadarLiveDirection.All;
}
