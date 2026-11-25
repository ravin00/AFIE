using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Features;

/// <summary>
/// Dims 47–54: cloud pricing + region signals. Order must match
/// src/ml/afie_ml/feature_names.py (dims 47–54) exactly.
/// </summary>
public sealed class CloudPricingFeatures : IFeatureGroup
{
    public int StartDim => 47;
    public int Length => 8;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        // shortcut: static per-workload profiles from config; swap for Azure Retail Prices API + K8s informer (stateful detection) in Phase 6/8.
        var opts = ctx.Options;
        var p = opts.CloudPricing.TryGetValue(ctx.WorkloadName, out var w) ? w : opts.DefaultCloudPricing;

        dest[0] = Unit(p.SpotPriceRatio);
        dest[1] = Unit(p.ReservationCoverage);
        dest[2] = (float)Math.Tanh(Math.Max(p.StorageAccessFrequencyPerHour, 0));
        dest[3] = Unit(p.RegionPriceIndex);
        dest[4] = p.CommitmentDiscountAvailable ? 1f : 0f;
        dest[5] = p.IsStateful ? 1f : 0f;
        dest[6] = Unit(p.CheaperRegionPriceIndex);
        dest[7] = (float)Math.Min(Math.Max(p.CrossRegionP99BaselineMs, 0) / 1000.0, 1.0); // same scale as latency dims 20–22
    }

    private static float Unit(double v) => (float)Math.Clamp(v, 0.0, 1.0);
}
