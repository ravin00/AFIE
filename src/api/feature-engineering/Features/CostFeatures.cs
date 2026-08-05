namespace AFIE.FeatureEngineering.Features;

public sealed class CostFeatures : IFeatureGroup
{
    private const double GiB = 1024.0 * 1024 * 1024;

    public int StartDim => 27;
    public int Length => 3;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        dest.Clear();
        if (ctx.Samples.Length == 0) return;

        var latest = ctx.Samples[^1];
        var opts = ctx.Options;

        var hourly = latest.CpuLimit * opts.CpuCostPerCoreHourUsd
                   + latest.MemLimit / GiB * opts.MemCostPerGiBHourUsd;

        dest[0] = (float)Math.Tanh(hourly / 10.0);
        dest[1] = 0f;  // 7-day trend — requires longer persistence; Phase 8
        dest[2] = (float)Math.Min(hourly / Math.Max(opts.ConfiguredBudgetUsdPerHour, 0.01), 1.0);
    }
}