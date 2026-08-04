using System.Text.Json;
using AFIE.Contracts;
using AFIE.FeatureEngineering.Consumers;
using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Consumers;

public class LocalJsonlTailConsumerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _inputDir;
    private readonly string _offsetPath;
    private readonly WindowStore _store;
    private readonly FeatureEngineeringHealthState _health;
    private readonly LocalJsonlTailConsumer _consumer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalJsonlTailConsumerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fe-consumer-" + Guid.NewGuid());
        _inputDir = Path.Combine(_tempDir, "results");
        _offsetPath = Path.Combine(_tempDir, "state", "offset.json");
        Directory.CreateDirectory(_inputDir);
        Directory.CreateDirectory(Path.GetDirectoryName(_offsetPath)!);

        var opts = Options.Create(new FeatureEngineeringOptions
        {
            WindowCapacity = 100,
            InputPath = _inputDir,
            OffsetStatePath = _offsetPath,
            PollingIntervalMs = 50
        });

        _store = new WindowStore(opts);
        _health = new FeatureEngineeringHealthState();
        _consumer = new LocalJsonlTailConsumer(_store, _health, opts, NullLogger<LocalJsonlTailConsumer>.Instance);
    }

    [Fact]
    public async Task Poll_NewEvents_ReachWindowStore()
    {
        WriteEvents(TodayPath(), Event("nginx"), Event("redis"), Event("nginx"));

        await RunOnceAsync();

        Assert.Equal(2, _store.WorkloadCount);
        Assert.Equal(2, _store.Snapshot("nginx")!.Length);
        Assert.Equal(1, _store.Snapshot("redis")!.Length);
        Assert.Equal(3, _health.EventsConsumedTotal);
        Assert.True(_health.SourceFileReachable);
    }

    [Fact]
    public async Task Poll_ResumesFromPersistedOffset()
    {
        WriteEvents(TodayPath(), Event("nginx"), Event("nginx"));
        await RunOnceAsync();
        Assert.Equal(2, _health.EventsConsumedTotal);

        WriteEvents(TodayPath(), Event("redis"));
        await RunOnceAsync();
        Assert.Equal(3, _health.EventsConsumedTotal);
        Assert.Equal(2, _store.WorkloadCount);
    }

    [Fact]
    public async Task Poll_MalformedLine_IsSkipped()
    {
        var path = TodayPath();
        File.WriteAllLines(path, new[]
        {
            JsonSerializer.Serialize(Event("nginx"), JsonOptions),
            "{not valid json",
            JsonSerializer.Serialize(Event("redis"), JsonOptions)
        });

        await RunOnceAsync();

        Assert.Equal(2, _health.EventsConsumedTotal);
    }

    [Fact]
    public async Task Poll_MissingFile_MarksHealthReachableTrue()
    {
        await RunOnceAsync();

        Assert.True(_health.SourceFileReachable);
        Assert.Equal(0, _health.EventsConsumedTotal);
    }

    private async Task RunOnceAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var runTask = _consumer.StartAsync(cts.Token);
        await runTask;
        await Task.Delay(250);
        await _consumer.StopAsync(CancellationToken.None);
    }

    private string TodayPath() =>
        Path.Combine(_inputDir, $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    private static void WriteEvents(string path, params MetricEvent[] events)
    {
        var lines = events.Select(e => JsonSerializer.Serialize(e, JsonOptions));
        File.AppendAllLines(path, lines);
    }

    private static MetricEvent Event(string workload) => new(
        workload, "afie-system", DateTimeOffset.UtcNow,
        CpuUsageRate: 0.5, MemoryBytes: 104857600,
        RequestRatePerSecond: 10, ErrorRatePct: 0,
        LatencyP50Ms: 5, LatencyP95Ms: 20, LatencyP99Ms: 50,
        NodeCpuPressure: false, NodeMemPressure: false,
        CpuRequest: 0.1, CpuLimit: 1.0,
        MemRequest: 67108864, MemLimit: 209715200);

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }
}