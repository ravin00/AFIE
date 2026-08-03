using AFIE.FeatureEngineering.Services;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Services;

public class CircularBufferTests
{
    [Fact]
    public void Add_UnderCapacity_SnapshotInInsertionOrder()
    {
        var buf = new CircularBuffer<int>(5);
        for (var i = 1; i <= 3; i++) buf.Add(i);
        Assert.Equal(new[] { 1, 2, 3 }, buf.Snapshot());
    }

    [Fact]
    public void Add_OverflowsCapacity_KeepsMostRecent()
    {
        var buf = new CircularBuffer<int>(3);
        for (var i = 1; i <= 6; i++) buf.Add(i);
        Assert.Equal(new[] { 4, 5, 6 }, buf.Snapshot());
    }

    [Fact]
    public void Snapshot_EmptyBuffer_ReturnsEmptyArray()
    {
        var buf = new CircularBuffer<int>(3);
        Assert.Empty(buf.Snapshot());
    }

    [Fact]
    public void Ctor_NonPositiveCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
    }
}
