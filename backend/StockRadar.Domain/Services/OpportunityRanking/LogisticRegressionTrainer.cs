namespace StockRadar.Domain.Services.OpportunityRanking;

/// <summary>Huấn luyện logistic regression offline — không phụ thuộc ML.NET.</summary>
public static class LogisticRegressionTrainer
{
    public sealed record TrainingResult(
        OpportunityRankerModel Model,
        int Samples,
        decimal Accuracy,
        decimal PositiveRate);

    public static TrainingResult Train(
        IReadOnlyList<(double[] Features, bool Label)> samples,
        int epochs = 800,
        double learningRate = 0.08,
        double l2 = 0.01)
    {
        var dim = OpportunityRankFeatures.Names.Length;
        if (samples.Count < 30)
            return new TrainingResult(OpportunityRankerModel.Untrained(), samples.Count, 0, 0);

        var weights = new double[dim];
        var intercept = 0.0;

        for (var epoch = 0; epoch < epochs; epoch++)
        {
            var gradW = new double[dim];
            var gradB = 0.0;

            foreach (var (x, y) in samples)
            {
                var p = PredictRaw(intercept, weights, x);
                var err = p - (y ? 1.0 : 0.0);
                gradB += err;
                for (var i = 0; i < dim; i++)
                    gradW[i] += err * x[i];
            }

            var n = samples.Count;
            intercept -= learningRate * gradB / n;
            for (var i = 0; i < dim; i++)
                weights[i] -= learningRate * (gradW[i] / n + l2 * weights[i]);
        }

        var correct = 0;
        var positives = 0;
        foreach (var (x, y) in samples)
        {
            if (y) positives++;
            var p = PredictRaw(intercept, weights, x);
            var pred = p >= 0.5;
            if (pred == y) correct++;
        }

        var accuracy = Math.Round(100m * correct / samples.Count, 1);
        var posRate = Math.Round(100m * positives / samples.Count, 1);

        return new TrainingResult(
            new OpportunityRankerModel
            {
                Intercept = intercept,
                Weights = weights,
                FeatureNames = OpportunityRankFeatures.Names,
                TrainingSamples = samples.Count,
                TrainingAccuracy = accuracy,
                TrainedAtUtc = DateTime.UtcNow,
            },
            samples.Count,
            accuracy,
            posRate);
    }

    private static double PredictRaw(double intercept, double[] weights, double[] x)
    {
        var z = intercept;
        for (var i = 0; i < weights.Length; i++)
            z += weights[i] * x[i];
        return 1.0 / (1.0 + Math.Exp(-z));
    }
}
