using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class MemoryFeaturesTests
{
    [Fact]
    public void FullBufferAtLimit_AllPercentilesAreOne()
    {
        var samples = Enumerable.Range(0, 240).Select(_ => new MetricEvent(
            "nginx", "ns", DateTimeOffset.UtcNow,
            0, 1_000_000_000, 0, 0, 0, 0, 0,
            false, false, 0, 0, 0, 1_000_000_000)).ToArray();

        var ctx = new FeatureContext("nginx", samples, DateTimeOffset.UtcNow,
            new FeatureEngineeringOptions(), Array.Empty<ActionRecord>());
        var buf = new float[9];
        new MemoryFeatures().Compute(ctx, buf);
        Assert.All(buf, v => Assert.InRange(v, 0.999f, 1.001f));
    }
}
