using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class ActionHistoryFeaturesTests
{
    private static FeatureContext Ctx(ActionRecord[] history, DateTimeOffset now) =>
        new("nginx", Array.Empty<MetricEvent>(), now,
            new FeatureEngineeringOptions(), history);

    [Fact]
    public void EmptyHistory_AllZeros()
    {
        var buf = new float[9];
        new ActionHistoryFeatures().Compute(Ctx(Array.Empty<ActionRecord>(), DateTimeOffset.UtcNow), buf);
        Assert.All(buf, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void ThreeRecords_PopulateSlotsMostRecentFirst()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var history = new[]
        {
            new ActionRecord("nginx", now.AddMinutes(-60), CostDelta: 0.3, SloDelta: -0.1),
            new ActionRecord("nginx", now.AddMinutes(-30), CostDelta: 0.5, SloDelta: -0.2),
            new ActionRecord("nginx", now.AddMinutes(-5),  CostDelta: 0.7, SloDelta: -0.3)
        };
        var buf = new float[9];
        new ActionHistoryFeatures().Compute(Ctx(history, now), buf);

        Assert.InRange(buf[0], 0.699f, 0.701f);
        Assert.InRange(buf[1], -0.301f, -0.299f);
        Assert.InRange(buf[2], 0.083f - 0.001f, 0.083f + 0.001f);

        Assert.InRange(buf[6], 0.299f, 0.301f);
        Assert.InRange(buf[8], 0.999f, 1.001f);
    }
}
