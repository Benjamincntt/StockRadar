using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfSessionRadarRepository(ApplicationDbContext db) : ISessionRadarRepository
{
    public async Task ReplaceSessionHitsAsync(
        DateOnly sessionDate,
        string exchange,
        IReadOnlyList<SessionRadarHitRecord> hits,
        DateTime scannedAt,
        CancellationToken cancellationToken = default)
    {
        var ex = exchange.Trim().ToUpperInvariant();
        var existing = await db.SessionRadarHits
            .Where(h => h.SessionDate == sessionDate && h.Exchange == ex)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            db.SessionRadarHits.RemoveRange(existing);

        if (hits.Count > 0)
        {
            db.SessionRadarHits.AddRange(hits.Select(h => new SessionRadarHitEntity
            {
                SessionDate = sessionDate,
                Exchange = ex,
                Symbol = h.Symbol,
                Name = h.Name,
                Sector = h.Sector,
                Price = h.Price,
                ChangePercent = h.ChangePercent,
                SessionVolume = h.SessionVolume,
                VolumeRatio = h.VolumeRatio,
                RelativeStrength = h.RelativeStrength,
                SignalsJson = JsonSerializer.Serialize(h.Signals, EntityMapper.JsonOptions),
                ScannedAt = scannedAt
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RadarLiveSnapshotDto> GetLiveSnapshotAsync(
        RadarLiveQuery query,
        CancellationToken cancellationToken = default)
    {
        var sessionDate = VietnamMarketCalendar.TodayVietnam();
        var items = await db.SessionRadarHits.AsNoTracking()
            .Where(h => h.SessionDate == sessionDate)
            .OrderByDescending(h => Math.Abs(h.ChangePercent))
            .ThenByDescending(h => h.SessionVolume)
            .ToListAsync(cancellationToken);

        var filtered = items
            .Where(h => h.SessionVolume >= query.MinSessionVolume)
            .Where(h => Math.Abs(h.ChangePercent) >= query.MinAbsChangePercent)
            .Where(h => query.Direction switch
            {
                RadarLiveDirection.Up => h.ChangePercent >= query.MinAbsChangePercent,
                RadarLiveDirection.Down => h.ChangePercent <= -query.MinAbsChangePercent,
                _ => true
            })
            .Select(ToDto)
            .ToList();

        var scannedAt = filtered.Count > 0
            ? filtered.Max(i => i.ScannedAt)
            : DateTime.UtcNow;

        var exchange = items.FirstOrDefault()?.Exchange ?? "HOSE";

        return new RadarLiveSnapshotDto(exchange, sessionDate, scannedAt, filtered.Count, filtered);
    }

    private static RadarLiveItemDto ToDto(SessionRadarHitEntity e)
    {
        var signals = JsonSerializer.Deserialize<List<string>>(e.SignalsJson, EntityMapper.JsonOptions) ?? [];
        return new RadarLiveItemDto(
            e.Symbol,
            e.Name,
            e.Sector,
            e.Price,
            e.ChangePercent,
            e.SessionVolume,
            e.VolumeRatio,
            e.RelativeStrength,
            signals,
            e.ScannedAt);
    }
}
