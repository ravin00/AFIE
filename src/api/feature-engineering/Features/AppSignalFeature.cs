namespace AFIE.FeatureEngineering.Features;

public sealed class AppSignalFeatures : IFeatureGroup
{
    public int StartDim => 18;
    public int Length => 6;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        dest.Clear();
        var slice = CpuFeatures.Slice(ctx.Samples, 20);
        if (slice.Length == 0) return;

        double reqSum = 0, errSum = 0, p50Sum = 0, p95Sum = 0, p99Sum = 0;
        foreach (var s in slice)
        {
            reqSum += s.RequestRatePerSecond;
            errSum += s.ErrorRatePct;
            p50Sum += s.LatencyP50Ms;
            p95Sum += s.LatencyP95Ms;
            p99Sum += s.LatencyP99Ms;
        }
        var n = slice.Length;

        dest[0] = (float)Math.Tanh(reqSum / n / 100.0);
        dest[1] = (float)(errSum / n / 100.0);
        dest[2] = (float)Math.Min(p50Sum / n / 1000.0, 1.0);
        dest[3] = (float)Math.Min(p95Sum / n / 1000.0, 1.0);
        dest[4] = (float)Math.Min(p99Sum / n / 1000.0, 1.0);
        // dest[5] reserved — 6th dim per workflow doc; future signal
    }
}