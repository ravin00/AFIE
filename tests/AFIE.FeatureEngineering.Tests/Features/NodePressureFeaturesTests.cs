using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class NodePressureFeaturesTests
{
    private static MetricEvent Event(bool cpuP, bool memP, double cpu, double cpuLim, long mem, double memLim) =>
        new("nginx", "ns", DateTimeOffset.UtcNow,
            CpuUsageRate: cpu, MemoryBytes: mem,
            RequestRatePerSecond: 0, ErrorRatePct: 0,
            LatencyP50Ms: 0, LatencyP95Ms: 0, LatencyP99Ms: 0,
            NodeCpuPressure: cpuP, NodeMemPressure: memP,
            CpuRequest: 0, CpuLimit: cpuLim, MemRequest: 0, MemLimit: memLim);

    private static FeatureContext Ctx(MetricEvent latest) =>
        new("nginx", new[] { latest }, DateTimeOffset.UtcNow,
            new FeatureEngineeringOptions(), Array.Empty<ActionRecord>());

    [Fact]
    public void BooleansMapToFlags_UtilTakesMax()
    {
        var buf = new float[3];
        new NodePressureFeatures().Compute(
            Ctx(Event(cpuP: true, memP: false, cpu: 0.3, cpuLim: 1.0, mem: 500_000_000, memLim: 1_000_000_000)),
            buf);
        Assert.Equal(1f, buf[0]);
        Assert.Equal(0f, buf[1]);
        Assert.InRange(buf[2], 0.499f, 0.501f);
    }

    [Fact]
    public void UtilClampsAtOne()
    {
        var buf = new float[3];
        new NodePressureFeatures().Compute(
            Ctx(Event(false, false, cpu: 5.0, cpuLim: 1.0, mem: 0, memLim: 1)),
            buf);
        Assert.Equal(1f, buf[2]);
    }

    [Fact]
    public void EmptySamples_AllZeros()
    {
        var buf = new float[3];
        var ctx = new FeatureContext("nginx", Array.Empty<MetricEvent>(),
            DateTimeOffset.UtcNow, new FeatureEngineeringOptions(), Array.Empty<ActionRecord>());
        new NodePressureFeatures().Compute(ctx, buf);
        Assert.All(buf, v => Assert.Equal(0f, v));
    }
}
