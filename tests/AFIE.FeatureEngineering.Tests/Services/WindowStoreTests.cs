using AFIE.Contracts;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Services;

public class WindowStoreTests
{
    private static WindowStore NewStore(int capacity = 5) =>
        new(Options.Create(new FeatureEngineeringOptions { WindowCapacity = capacity }));

    private static MetricEvent Event(string name, DateTimeOffset ts) =>
        new(name, "ns", ts, 0, 0, 0, 0, 0, 0, 0, false, false, 0, 0, 0, 0);

    [Fact]
    public void Add_UnknownWorkload_CreatesBufferOnDemand()
    {
        var store = NewStore();
        store.Add(Event("nginx", DateTimeOffset.UtcNow));
        Assert.Equal(1, store.WorkloadCount);
        Assert.Single(store.Snapshot("nginx")!);
    }

    [Fact]
    public void Snapshot_UnknownWorkload_ReturnsNull()
    {
        Assert.Null(NewStore().Snapshot("missing"));
    }

    [Fact]
    public void ConcurrentAdds_AllEventsRetained()
    {
        var store = NewStore(capacity: 1000);
        Parallel.For(0, 500, i => store.Add(Event("nginx", DateTimeOffset.UtcNow)));
        Parallel.For(0, 500, i => store.Add(Event("redis", DateTimeOffset.UtcNow)));
        Assert.Equal(500, store.Snapshot("nginx")!.Length);
        Assert.Equal(500, store.Snapshot("redis")!.Length);
    }
}
