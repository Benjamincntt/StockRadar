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

        var positiveCount = samples.Count(s => s.Label);
        var negativeCount = samples.Count - positiveCount;

        // Class weights — giảm bias khi positive rate thấp (<30%).
        var posWeight = negativeCount > 0 && positiveCount > 0
            ? (double)negativeCount / positiveCount
            : 1.0;
        posWeight = Math.Min(posWeight, 5.0); // cap tối đa 5× để tránh overshoot

        var weights = new double[dim];
        var intercept = 0.0;

        for (var epoch = 0; epoch < epochs; epoch++)
        {
            var gradW = new double[dim];
            var gradB = 0.0;

            foreach (var (x, y) in samples)
            {
                var p = PredictRaw(intercept, weights, x);
                var err = (p - (y ? 1.0 : 0.0)) * (y ? posWeight : 1.0);
                gradB += err;
                for (var i = 0; i < dim; i++)
                    gradW[i] += err * x[i];
            }

            var effectiveN = positiveCount * posWeight + negativeCount;
            intercept -= learningRate * gradB / effectiveN;
            for (var i = 0; i < dim; i++)
                weights[i] -= learningRate * (gradW[i] / effectiveN + l2 * weights[i]);
        }

        // Đo AUC (Mann-Whitney U) trên tập train để log.
        var auc = ComputeAuc(intercept, weights, samples);
        var posRate = Math.Round(100m * positiveCount / samples.Count, 1);

        return new TrainingResult(
            new OpportunityRankerModel
            {
                Intercept = intercept,
                Weights = weights,
                FeatureNames = OpportunityRankFeatures.Names,
                TrainingSamples = samples.Count,
                TrainingAccuracy = auc,
                TrainedAtUtc = DateTime.UtcNow,
                Version = "logistic-v2",
            },
            samples.Count,
            auc,
            posRate);
    }

    /// <summary>
    /// AUC-ROC bằng Mann-Whitney U: P(score_pos > score_neg).
    /// Trả về 0–100 (%) để khớp với TrainingAccuracy ngữ nghĩa cũ.
    /// 50 = random, 60+ = hữu ích, 70+ = tốt.
    /// </summary>
    public static decimal ComputeAuc(
        double intercept,
        double[] weights,
        IReadOnlyList<(double[] Features, bool Label)> samples)
    {
        var posScores = new List<double>();
        var negScores = new List<double>();
        foreach (var (x, y) in samples)
        {
            var p = PredictRaw(intercept, weights, x);
            if (y) posScores.Add(p);
            else negScores.Add(p);
        }

        if (posScores.Count == 0 || negScores.Count == 0)
            return 50m;

        var wins = 0L;
        foreach (var ps in posScores)
            foreach (var ns in negScores)
            {
                if (ps > ns) wins++;
                else if (ps == ns) wins++; // tie = 0.5, sum at end
            }

        // Điều chỉnh tie về 0.5
        var ties = 0L;
        foreach (var ps in posScores)
            foreach (var ns in negScores)
                if (ps == ns) ties++;

        var auc = (wins - ties * 0.5) / ((double)posScores.Count * negScores.Count);
        return Math.Round((decimal)auc * 100m, 1);
    }

    internal static double PredictRaw(double intercept, double[] weights, double[] x)
    {
        var z = intercept;
        for (var i = 0; i < weights.Length; i++)
            z += weights[i] * x[i];
        return 1.0 / (1.0 + Math.Exp(-z));
    }
}
