using System.Text.Json;
using AFIE.Telemetry.Models;

namespace AFIE.Telemetry.Tests.Models;

public class MetricEventTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static MetricEvent CreateSample() => new(
        WorkloadName: "nginx-deployment",
        Namespace: "baseline-afie",
        Timestamp: DateTimeOffset.Parse("2025-07-01T12:00:00Z"),
        CpuUsageRate: 0.25,
        MemoryBytes: 134217728,
        RequestRatePerSecond: 150.5,
        ErrorRatePct: 1.2,
        LatencyP50Ms: 12.5,
        LatencyP95Ms: 45.0,
        LatencyP99Ms: 120.0,
        NodeCpuPressure: false,
        NodeMemPressure: false,
        CpuRequest: 0.1,
        CpuLimit: 0.5,
        MemRequest: 67108864,
        MemLimit: 134217728
    );

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = CreateSample();
        var b = CreateSample();
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = CreateSample();
        var b = a with { CpuUsageRate = 0.99 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void JsonRoundTrip_AllFieldsPreserved()
    {
        var original = CreateSample();
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MetricEvent>(json, JsonOptions);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void JsonSerialization_UsesCamelCase()
    {
        var evt = CreateSample();
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        Assert.Contains("\"workloadName\"", json);
        Assert.Contains("\"cpuUsageRate\"", json);
        Assert.DoesNotContain("\"WorkloadName\"", json);
    }
}
