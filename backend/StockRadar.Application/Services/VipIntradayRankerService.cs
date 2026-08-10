using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class VipIntradayRankerService(
    IVipIntradayRankerModelStore modelStore,
    IOptions<MasterAlertOptions> options) : IVipIntradayRanker
{
    private OpportunityRankerModel _model = OpportunityRankerModel.Untrained();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _loaded;

    public bool IsModelActive =>
        options.Value.IntradayModelEnabled
        && _model.IsTrained
        && FeatureNamesMatch(_model.FeatureNames);

    private static bool FeatureNamesMatch(string[] names) =>
        names.Length == VipIntradayFeatures.Names.Length
        && names.Zip(VipIntradayFeatures.Names).All(p => p.First == p.Second);

    public OpportunityRankerModel GetModelSnapshot() => _model;

    public decimal PredictWinProbability(VipIntradayInput input)
    {
        EnsureLoaded();
        if (!IsModelActive)
            return 50m;

        var features = VipIntradayFeatures.Vectorize(input);
        var p = _model.PredictProbability(features);
        if (double.IsNaN(p))
            return 50m;

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
