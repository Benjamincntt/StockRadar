using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class OpportunityRankerService(
    IOpportunityRankerModelStore modelStore,
    IOptions<OpportunityRankerOptions> options) : IOpportunityRanker
{
    private OpportunityRankerModel _model = OpportunityRankerModel.Untrained();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _loaded;

    public bool IsModelActive =>
        options.Value.Enabled && _model.IsTrained;

    public OpportunityRankerModel GetModelSnapshot() => _model;

    public decimal PredictWinProbability(OpportunityRankInput input)
    {
        EnsureLoaded();
        var cfg = options.Value;

        if (!cfg.Enabled || (!_model.IsTrained && cfg.FallbackToLegacyHit))
            return input.PredictedHitPercent;

        var features = OpportunityRankFeatures.Vectorize(input);
        var p = _model.PredictProbability(features);
        if (double.IsNaN(p))
            return cfg.FallbackToLegacyHit ? input.PredictedHitPercent : 50m;

        return Math.Clamp((decimal)Math.Round(p * 100.0, 1), 5m, 95m);
    }

    public Task ReloadModelAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    internal async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _model = await modelStore.LoadAsync(cancellationToken);
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    internal void SetModel(OpportunityRankerModel model)
    {
        _model = model;
        _loaded = true;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loadLock.Wait();
        try
        {
            if (_loaded)
                return;
            _model = modelStore.LoadAsync().GetAwaiter().GetResult();
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
