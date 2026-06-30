using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfFalsePositiveMiningRepository(ApplicationDbContext db) : IFalsePositiveMiningRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<FalsePositiveMiningResult?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.FalsePositiveMiningStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (state is null)
            return null;

        var penalties = JsonSerializer.Deserialize<List<FalsePositiveCriterionPenalty>>(state.ResultsJson, JsonOptions)
            ?? [];
        return new FalsePositiveMiningResult(state.FalsePositiveSetups, state.GoodSetups, penalties);
    }

    public async Task SaveAsync(FalsePositiveMiningResult result, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(result.Penalties, JsonOptions);
        var now = DateTime.UtcNow;
        var state = await db.FalsePositiveMiningStates.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (state is null)
        {
            db.FalsePositiveMiningStates.Add(new FalsePositiveMiningStateEntity
            {
                Id = 1,
                FalsePositiveSetups = result.FalsePositiveSetups,
                GoodSetups = result.GoodSetups,
                ResultsJson = json,
                UpdatedAt = now,
            });
        }
        else
        {
            state.FalsePositiveSetups = result.FalsePositiveSetups;
            state.GoodSetups = result.GoodSetups;
            state.ResultsJson = json;
            state.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
