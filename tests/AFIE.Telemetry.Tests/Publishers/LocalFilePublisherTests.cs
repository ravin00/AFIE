using System.Text.Json;
using AFIE.Telemetry.Models;
using AFIE.Telemetry.Publishers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AFIE.Telemetry.Tests.Publishers;

public class LocalFilePublisherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFilePublisher _publisher;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalFilePublisherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"afie_test_{Guid.NewGuid():N}");
        var options = Options.Create(new TelemetryOptions { OutputPath = _tempDir });
        var logger = Mock.Of<ILogger<LocalFilePublisher>>();
        _publisher = new LocalFilePublisher(options, logger);
    }

    [Fact]
    public async Task PublishAsync_WritesJsonlFile()
    {
        var events = new List<MetricEvent> { CreateEvent("nginx") };
        await _publisher.PublishAsync(events);

        var files = Directory.GetFiles(_tempDir, "telemetry_*.jsonl");
        Assert.Single(files);

        var lines = await File.ReadAllLinesAsync(files[0]);
        Assert.Single(lines);
    }

    [Fact]
    public async Task PublishAsync_AppendsToExistingFile()
    {
        var batch1 = new List<MetricEvent> { CreateEvent("nginx") };
        var batch2 = new List<MetricEvent> { CreateEvent("redis"), CreateEvent("postgres") };

        await _publisher.PublishAsync(batch1);
        await _publisher.PublishAsync(batch2);

        var files = Directory.GetFiles(_tempDir, "telemetry_*.jsonl");
        Assert.NotEmpty(files);

        var allLines = (await Task.WhenAll(files.Select(f => File.ReadAllLinesAsync(f))))
            .SelectMany(x => x)
            .ToArray();

        Assert.Equal(3, allLines.Length);
    }

    [Fact]
    public async Task PublishAsync_EachLineIsValidJson()
    {
        var events = new List<MetricEvent> { CreateEvent("nginx"), CreateEvent("redis") };
        await _publisher.PublishAsync(events);

        var files = Directory.GetFiles(_tempDir, "telemetry_*.jsonl");
        var lines = await File.ReadAllLinesAsync(files[0]);

        foreach (var line in lines)
        {
            var parsed = JsonSerializer.Deserialize<MetricEvent>(line, JsonOptions);
            Assert.NotNull(parsed);
            Assert.NotEmpty(parsed!.WorkloadName);
        }
    }

    [Fact]
    public async Task PublishAsync_EmptyList_WritesNothing()
    {
        await _publisher.PublishAsync(new List<MetricEvent>());
        Assert.False(Directory.Exists(_tempDir));
    }

    private static MetricEvent CreateEvent(string workload) => new(
        WorkloadName: workload,
        Namespace: "default",
        Timestamp: DateTimeOffset.UtcNow,
        CpuUsageRate: 0.1,
        MemoryBytes: 1024,
        RequestRatePerSecond: 10,
        ErrorRatePct: 0,
        LatencyP50Ms: 5,
        LatencyP95Ms: 20,
        LatencyP99Ms: 50,
        NodeCpuPressure: false,
        NodeMemPressure: false,
        CpuRequest: 0.1,
        CpuLimit: 0.5,
        MemRequest: 1024,
        MemLimit: 2048
    );

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
