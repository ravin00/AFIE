using AFIE.Contracts;
using MathNet.Numerics.Statistics;

namespace AFIE.FeatureEngineering.Features;

public sealed class MemoryFeatures : IFeatureGroup
{
    private const double Eps = 1e-6;
    private static readonly int[] Windows = { 20, 60, 240 };
    private static readonly double[] Percentiles = { 0.50, 0.95, 0.99 };

    public int StartDim => 9;
    public int Length => 9;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        var samples = ctx.Samples;
        var idx = 0;
        foreach (var window in Windows)
        {
            var slice = CpuFeatures.Slice(samples, window);
            foreach (var p in Percentiles)
                dest[idx++] = (float)Percentile(slice, p);
        }
    }

    private static double Percentile(MetricEvent[] slice, double percentile)
    {
        if (slice.Length < 3) return 0;
        var ratios = new double[slice.Length];
        for (var i = 0; i < slice.Length; i++)
            ratios[i] = slice[i].MemoryBytes / Math.Max(slice[i].MemLimit, Eps);
        return Statistics.Percentile(ratios, (int)(percentile * 100));
    }
}