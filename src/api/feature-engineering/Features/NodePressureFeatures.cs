namespace AFIE.FeatureEngineering.Features;

public sealed class NodePressureFeatures : IFeatureGroup
{
    private const double Eps = 1e-6;

    public int StartDim => 24;
    public int Length => 3;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        dest.Clear();
        if (ctx.Samples.Length == 0) return;

        var latest = ctx.Samples[^1];
        dest[0] = latest.NodeCpuPressure ? 1f : 0f;
        dest[1] = latest.NodeMemPressure ? 1f : 0f;

        var cpuUtil = latest.CpuUsageRate / Math.Max(latest.CpuLimit, Eps);
        var memUtil = latest.MemoryBytes / Math.Max(latest.MemLimit, Eps);
        dest[2] = (float)Math.Min(Math.Max(cpuUtil, memUtil), 1.0);
    }
}