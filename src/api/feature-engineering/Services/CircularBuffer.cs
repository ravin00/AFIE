namespace AFIE.FeatureEngineering.Services;

public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public int Capacity { get; }
    public int Count { get { lock (_lock) return _count; } }

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }
    }

    public T[] Snapshot()
    {
        lock (_lock)
        {
            var result = new T[_count];
            var start = _count < Capacity ? 0 : _head;
            for (var i = 0; i < _count; i++)
                result[i] = _buffer[(start + i) % Capacity];
            return result;
        }
    }
}
