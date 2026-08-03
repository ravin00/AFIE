using System.Collections.Concurrent;
using AFIE.Contracts;
using AFIE.FeatureEngineering.Models;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Services;

public sealed class WindowStore
{
    private readonly ConcurrentDictionary<string, CircularBuffer<MetricEvent>> _buffers = new();
    private readonly int _capacity;

    public WindowStore(IOptions<FeatureEngineeringOptions> options)
    {
        _capacity = options.Value.WindowCapacity;
    }

    public void Add(MetricEvent evt)
    {
        var buffer = _buffers.GetOrAdd(evt.WorkloadName, _ => new CircularBuffer<MetricEvent>(_capacity));
        buffer.Add(evt);
    }

    public MetricEvent[]? Snapshot(string workloadName)
    {
        return _buffers.TryGetValue(workloadName, out var buffer) ? buffer.Snapshot() : null;
    }

    public IReadOnlyCollection<string> Workloads => _buffers.Keys.ToArray();
    public int WorkloadCount => _buffers.Count;
}
