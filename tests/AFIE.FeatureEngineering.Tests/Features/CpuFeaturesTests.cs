using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class CpuFeaturesTests
{
    private static MetricEvent Event(double cpu, double limit) =>
        new("nginx", "ns", DateTimeOffset.UtcNow,
            CpuUsageRate: cpu, MemoryBytes: 0,
            RequestRatePerSecond: 0, ErrorRatePct: 0,
            LatencyP50Ms: 0, LatencyP95Ms: 0, LatencyP99Ms: 0,
            NodeCpuPressure: false, NodeMemPressure: false,
            CpuRequest: 0, CpuLimit: limit, MemRequest: 0, MemLimit: 0);

    private static FeatureContext Ctx(MetricEvent[] samples) =>
        new("nginx", samples, DateTimeOffset.UtcNow, new FeatureEngineeringOptions(), Array.Empty<ActionRecord>());

    [Fact]
    public void FullBufferAtLimit_AllPercentilesAreOne()
    {
        var samples = Enumerable.Range(0, 240).Select(_ => Event(1.0, 1.0)).ToArray();
        var buf = new float[9];
        new CpuFeatures().Compute(Ctx(samples), buf);
        Assert.All(buf, v => Assert.InRange(v, 0.999f, 1.001f));
    }

    [Fact]
    public void EmptySamples_AllZeros()
    {
        var buf = new float[9];
        new CpuFeatures().Compute(Ctx(Array.Empty<MetricEvent>()), buf);
        Assert.All(buf, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void FewerThanThreeSamples_AllZeros()
    {
        var samples = new[] { Event(0.5, 1.0), Event(0.7, 1.0) };
        var buf = new float[9];
        new CpuFeatures().Compute(Ctx(samples), buf);
        Assert.All(buf, v => Assert.Equal(0f, v));
    }
}
