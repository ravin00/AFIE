using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class StateVectorBuilderTests
{
    private static StateVectorBuilder Build(IEnumerable<IFeatureGroup>? groups = null)
    {
        var options = Options.Create(new FeatureEngineeringOptions());
        groups ??= new IFeatureGroup[]
        {
            new CpuFeatures(), new MemoryFeatures(), new AppSignalFeatures(),
            new NodePressureFeatures(), new CostFeatures(), new TemporalFeatures(),
            new DeploymentFeatures(), new ActionHistoryFeatures()
        };
        return new StateVectorBuilder(groups, options, new ActionHistoryStore());
    }

    [Fact]
    public void Build_ProducesExactly47Values()
    {
        var vector = Build().Build("nginx", Array.Empty<MetricEvent>(), DateTimeOffset.UtcNow);
        Assert.Equal(47, vector.Values.Length);
    }

    [Fact]
    public void Build_AllValuesFinite()
    {
        var vector = Build().Build("nginx", Array.Empty<MetricEvent>(), DateTimeOffset.UtcNow);
        Assert.All(vector.Values, v => Assert.True(!float.IsNaN(v) && !float.IsInfinity(v)));
    }

    [Fact]
    public void Build_ClampsAndCoercesNaNInf()
    {
        var vector = Build(new IFeatureGroup[] { new BadGroup(0, 47) })
            .Build("nginx", Array.Empty<MetricEvent>(), DateTimeOffset.UtcNow);

        Assert.Equal(0f, vector.Values[0]);   // NaN -> 0
        Assert.Equal(0f, vector.Values[1]);   // +Inf -> 0 (coerced, not clamped)
        Assert.Equal(0f, vector.Values[2]);   // -Inf -> 0
        Assert.Equal(2f, vector.Values[3]);   // 5.0 -> clamp to 2
    }

    [Fact]
    public void Ctor_ThrowsWhenGroupsDoNotSumTo47()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Build(new IFeatureGroup[] { new CpuFeatures() }));
    }

    [Fact]
    public void Ctor_ThrowsWhenGroupsHaveGap()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Build(new IFeatureGroup[]
            {
                new CpuFeatures(),
                new BadGroup(startDim: 20, length: 27)
            }));
    }

    [Fact]
    public void Ctor_NullValues_Throws()
    => Assert.Throws<ArgumentNullException>(() =>
        new StateVector("w", "ns", DateTimeOffset.UtcNow, null!));

    [Theory]
    [InlineData(0)]
    [InlineData(46)]
    [InlineData(48)]
    public void Ctor_WrongLength_Throws(int len)
        => Assert.Throws<ArgumentException>(() =>
            new StateVector("w", "ns", DateTimeOffset.UtcNow, new float[len]));

    private sealed class BadGroup : IFeatureGroup
    {
        public int StartDim { get; }
        public int Length { get; }
        public BadGroup(int startDim, int length) { StartDim = startDim; Length = length; }
        public void Compute(FeatureContext ctx, Span<float> dest)
        {
            if (dest.Length > 0) dest[0] = float.NaN;
            if (dest.Length > 1) dest[1] = float.PositiveInfinity;
            if (dest.Length > 2) dest[2] = float.NegativeInfinity;
            if (dest.Length > 3) dest[3] = 5.0f;
            for (var i = 4; i < dest.Length; i++) dest[i] = 0f;
        }
    }
}
