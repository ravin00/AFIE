using System.Collections.Concurrent;
using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Services;

public sealed class ActionHistoryStore
{
    private const int Capacity = 3;
    private readonly ConcurrentDictionary<string, CircularBuffer<ActionRecord>> _buffers = new();

    public void Record(ActionRecord record)
    {
        var buffer = _buffers.GetOrAdd(record.WorkloadName, _ => new CircularBuffer<ActionRecord>(Capacity));
        buffer.Add(record);
    }

    public ActionRecord[] Recent(string workloadName)
    {
        return _buffers.TryGetValue(workloadName, out var buffer) ? buffer.Snapshot() : Array.Empty<ActionRecord>();
    }
}
