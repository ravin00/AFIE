namespace AFIE.Telemetry.Queries;

public static class PrometheusQueries
{
    public const string CpuUsageRate =
        "sum(rate(container_cpu_usage_seconds_total{container!=\"\",container!=\"POD\"}[5m])) by (pod, namespace)";

    public const string MemoryBytes =
        "sum(container_memory_working_set_bytes{container!=\"\",container!=\"POD\"}) by (pod, namespace)";

    public const string RequestRate =
        "sum(rate(http_requests_total[5m])) by (pod, namespace)";

    public const string ErrorRate =
        "sum(rate(http_requests_total{code=~\"5..\"}[5m])) by (pod, namespace) / sum(rate(http_requests_total[5m])) by (pod, namespace) * 100";

    public const string LatencyP50 =
        "histogram_quantile(0.50, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, pod, namespace))";

    public const string LatencyP95 =
        "histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, pod, namespace))";

    public const string LatencyP99 =
        "histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, pod, namespace))";

    public const string CpuRequests =
        "kube_pod_container_resource_requests{resource=\"cpu\"}";

    public const string CpuLimits =
        "kube_pod_container_resource_limits{resource=\"cpu\"}";

    public const string MemRequests =
        "kube_pod_container_resource_requests{resource=\"memory\"}";

    public const string MemLimits =
        "kube_pod_container_resource_limits{resource=\"memory\"}";

    public const string NodeCpuPressure =
        "kube_node_status_condition{condition=\"PIDPressure\",status=\"true\"}";

    public const string NodeMemPressure =
        "kube_node_status_condition{condition=\"MemoryPressure\",status=\"true\"}";

    public static Dictionary<string, string> AllInstantQueries() => new()
    {
        [nameof(CpuUsageRate)] = CpuUsageRate,
        [nameof(MemoryBytes)] = MemoryBytes,
        [nameof(RequestRate)] = RequestRate,
        [nameof(ErrorRate)] = ErrorRate,
        [nameof(LatencyP50)] = LatencyP50,
        [nameof(LatencyP95)] = LatencyP95,
        [nameof(LatencyP99)] = LatencyP99,
        [nameof(CpuRequests)] = CpuRequests,
        [nameof(CpuLimits)] = CpuLimits,
        [nameof(MemRequests)] = MemRequests,
        [nameof(MemLimits)] = MemLimits,
        [nameof(NodeCpuPressure)] = NodeCpuPressure,
        [nameof(NodeMemPressure)] = NodeMemPressure,
    };
}