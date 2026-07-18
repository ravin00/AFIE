using System.Text.RegularExpressions;
using AFIE.Telemetry.Clients;
using AFIE.Telemetry.Models;
using AFIE.Telemetry.Publishers;
using AFIE.Telemetry.Queries;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Services;

public partial class PrometheusScraperService : BackgroundService
{
    private readonly PrometheusHttpClient _prometheus;
    private readonly IMetricPublisher _publisher;
    private readonly TelemetryHealthState _healthState;
    private readonly TelemetryOptions _options;
    private readonly ILogger<PrometheusScraperService> _logger;

    public PrometheusScraperService(
        PrometheusHttpClient prometheus,
        IMetricPublisher publisher,
        TelemetryHealthState healthState,
        IOptions<TelemetryOptions> options,
        ILogger<PrometheusScraperService> logger)
    {
        _prometheus = prometheus;
        _publisher = publisher;
        _healthState = healthState;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraper starting, interval: {Interval}s", _options.ScrapingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var results = await FetchAllMetricsAsync(stoppingToken);
                var events = AssembleMetricEvents(results);
                await _publisher.PublishAsync(events, stoppingToken);

                _healthState.LastScrapeTime = DateTimeOffset.UtcNow;
                _healthState.EventsPublishedTotal += events.Count;
                _healthState.PrometheusReachable = true;

                _logger.LogInformation("Scrape complete: {Count} events", events.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Scrape cycle failed");
                _healthState.PrometheusReachable = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.ScrapingIntervalSeconds), stoppingToken);
        }
    }

    private async Task<Dictionary<string, List<PrometheusResult>>> FetchAllMetricsAsync(CancellationToken ct)
    {
        var queries = PrometheusQueries.AllInstantQueries();
        var tasks = queries.Select(async kvp =>
        {
            var results = await _prometheus.QueryAsync(kvp.Value, ct);
            return (kvp.Key, results);
        });

        var completed = await Task.WhenAll(tasks);
        return completed.ToDictionary(x => x.Key, x => x.results);
    }

    private List<MetricEvent> AssembleMetricEvents(Dictionary<string, List<PrometheusResult>> results)
    {
        var podKeys = results.Values
            .SelectMany(r => r)
            .Where(r => r.Metric.ContainsKey("pod") && r.Metric.ContainsKey("namespace"))
            .Select(r => (Pod: r.Metric["pod"], Namespace: r.Metric["namespace"]))
            .Distinct()
            .ToList();

        var events = new List<MetricEvent>();

        foreach (var (pod, ns) in podKeys)
        {
            events.Add(new MetricEvent(
                WorkloadName: DeriveWorkloadName(pod),
                Namespace: ns,
                Timestamp: DateTimeOffset.UtcNow,
                CpuUsageRate: GetValue(results, "CpuUsageRate", pod, ns),
                MemoryBytes: (long)GetValue(results, "MemoryBytes", pod, ns),
                RequestRatePerSecond: GetValue(results, "RequestRate", pod, ns),
                ErrorRatePct: GetValue(results, "ErrorRate", pod, ns),
                LatencyP50Ms: GetValue(results, "LatencyP50", pod, ns) * 1000,
                LatencyP95Ms: GetValue(results, "LatencyP95", pod, ns) * 1000,
                LatencyP99Ms: GetValue(results, "LatencyP99", pod, ns) * 1000,
                NodeCpuPressure: GetValue(results, "NodeCpuPressure", pod, ns) > 0,
                NodeMemPressure: GetValue(results, "NodeMemPressure", pod, ns) > 0,
                CpuRequest: GetValue(results, "CpuRequests", pod, ns),
                CpuLimit: GetValue(results, "CpuLimits", pod, ns),
                MemRequest: GetValue(results, "MemRequests", pod, ns),
                MemLimit: GetValue(results, "MemLimits", pod, ns)
            ));
        }

        return events;
    }

    private static double GetValue(
        Dictionary<string, List<PrometheusResult>> results, string queryName, string pod, string ns)
    {
        if (!results.TryGetValue(queryName, out var queryResults))
            return 0;

        var match = queryResults.FirstOrDefault(r =>
            r.Metric.TryGetValue("pod", out var p) && p == pod &&
            r.Metric.TryGetValue("namespace", out var n) && n == ns);

        if (match?.Value is [_, var valObj] &&
            double.TryParse(valObj?.ToString(), out var val) &&
            !double.IsNaN(val))
            return val;

        return 0;
    }

    private static string DeriveWorkloadName(string podName)
    {
        var rsMatch = ReplicaSetPodRegex().Match(podName);
        if (rsMatch.Success) return rsMatch.Groups[1].Value;

        var stsMatch = StatefulSetPodRegex().Match(podName);
        if (stsMatch.Success) return stsMatch.Groups[1].Value;

        return podName;
    }

    [GeneratedRegex(@"^(.+)-[a-f0-9]{6,10}-[a-z0-9]{5}$")]
    private static partial Regex ReplicaSetPodRegex();

    [GeneratedRegex(@"^(.+)-\d+$")]
    private static partial Regex StatefulSetPodRegex();
}
