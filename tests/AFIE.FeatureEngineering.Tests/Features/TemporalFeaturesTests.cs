using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class TemporalFeaturesTests
{
    private static FeatureContext Ctx(DateTimeOffset now) =>
        new("w", Array.Empty<MetricEvent>(), now, new FeatureEngineeringOptions(), Array.Empty<ActionRecord>());

    [Fact]
    public void Midnight_SinZero_CosOne()
    {
        var buf = new float[5];
        new TemporalFeatures().Compute(Ctx(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), buf);
        Assert.InRange(buf[0], -0.001f, 0.001f);
        Assert.InRange(buf[1], 0.999f, 1.001f);
    }

    [Fact]
    public void HourNearMidnight_AdjacentInFeatureSpace()
    {
        var buf0 = new float[5];
        var buf23 = new float[5];
        var group = new TemporalFeatures();
        group.Compute(Ctx(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), buf0);
        group.Compute(Ctx(new DateTimeOffset(2025, 12, 31, 23, 59, 55, TimeSpan.Zero)), buf23);
        Assert.True(Math.Abs(buf0[0] - buf23[0]) < 0.01f);
        Assert.True(Math.Abs(buf0[1] - buf23[1]) < 0.01f);
    }

    [Fact]
    public void DayOfWeek_SundayNearSaturday_AdjacentInFeatureSpace()
    {
        var buf0 = new float[5];
        var buf1 = new float[5];
        var group = new TemporalFeatures();
        var sundayMidnight = new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero);
        var saturdayLate  = new DateTimeOffset(2026, 1, 3, 23, 59, 55, TimeSpan.Zero);
        group.Compute(Ctx(sundayMidnight), buf0);
        group.Compute(Ctx(saturdayLate), buf1);
        Assert.True(Math.Abs(buf0[2] - buf1[2]) < 0.01f);
        Assert.True(Math.Abs(buf0[3] - buf1[3]) < 0.01f);
    }
}
