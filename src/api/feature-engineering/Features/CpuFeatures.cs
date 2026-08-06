using AFIE.Contracts;
using MathNet.Numerics.Statistics;

namespace AFIE.FeatureEngineering.Features;

public sealed class CpuFeatures : IFeatureGroup
{
    private const double Eps = 1e-6;
    private static readonly int[] Windows = { 20, 60, 240 };  // 5m / 15m / 1h at 15s cadence
    private static readonly double[] Percentiles = { 0.50, 0.95, 0.99 };

    public int StartDim => 0;
    public int Length => 9;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        var samples = ctx.Samples;
        var idx = 0;
        foreach (var window in Windows)
        {
            var slice = Slice(samples, window);
            foreach (var p in Percentiles)
                dest[idx++] = (float)Percentile(slice, p);
        }
    }

    private static double Percentile(MetricEvent[] slice, double percentile)
    {
        if (slice.Length < 3) return 0;
        var ratios = new double[slice.Length];
        for (var i = 0; i < slice.Length; i++)
            ratios[i] = slice[i].CpuUsageRate / Math.Max(slice[i].CpuLimit, Eps);
        return Statistics.Percentile(ratios, (int)(percentile * 100));
    }

    internal static MetricEvent[] Slice(MetricEvent[] samples, int window)
    {
        if (samples.Length <= window) return samples;
        var result = new MetricEvent[window];
        Array.Copy(samples, samples.Length - window, result, 0, window);
        return result;
    }
}