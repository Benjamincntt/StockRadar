namespace StockRadar.Domain.Services.OpportunityRanking;

public sealed class OpportunityRankerModel
{
    public double Intercept { get; init; }
    public double[] Weights { get; init; } = [];
    public string[] FeatureNames { get; init; } = OpportunityRankFeatures.Names;
    public int TrainingSamples { get; init; }
    public decimal TrainingAccuracy { get; init; }
    public DateTime? TrainedAtUtc { get; init; }
    public string Version { get; init; } = "logistic-v1";

    public bool IsTrained =>
        Weights.Length == OpportunityRankFeatures.Names.Length && TrainingSamples >= 30;

    public double PredictProbability(IReadOnlyList<double> features)
    {
        if (!IsTrained || features.Count != Weights.Length)
            return double.NaN;

        var z = Intercept;
        for (var i = 0; i < Weights.Length; i++)
            z += Weights[i] * features[i];

        return Sigmoid(z);
    }

    public static OpportunityRankerModel Untrained() => new();

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
}
