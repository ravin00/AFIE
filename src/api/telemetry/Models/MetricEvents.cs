namespace AFIE.Telemetry.Models;

public record MetricEvent(
    string WorkloadName,
    string Namespace,
    DateTimeOffset Timestamp,
    double CpuUsageRate,
    long MemoryBytes,
    double RequestRatePerSecond,
    double ErrorRatePct,
    double LatencyP50Ms,
    double LatencyP95Ms,
    double LatencyP99Ms,
    bool NodeCpuPressure,
    bool NodeMemPressure,
    double CpuRequest,
    double CpuLimit,
    double MemRequest,
    double MemLimit
); 