using AFIE.Telemetry.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Health;

public class TelemetryHealthCheck : IHealthCheck
{
    private readonly TelemetryHealthState _state;
    private readonly TelemetryOptions _options;

    public TelemetryHealthCheck(TelemetryHealthState state, IOptions<TelemetryOptions> options)
    {
        _state = state;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var data = new Dictionary<string, object>
        {
            ["lastScrapeTime"] = _state.LastScrapeTime?.ToString("o") ?? "never",
            ["eventsPublishedTotal"] = _state.EventsPublishedTotal,
            ["prometheusReachable"] = _state.PrometheusReachable
        };

        if (_state.LastScrapeTime is null)
            return Task.FromResult(HealthCheckResult.Degraded("No scrape completed yet", data: data));

        var staleness = DateTimeOffset.UtcNow - _state.LastScrapeTime.Value;
        var threshold = TimeSpan.FromSeconds(_options.ScrapingIntervalSeconds * 2);

        if (staleness > threshold || !_state.PrometheusReachable)
            return Task.FromResult(HealthCheckResult.Degraded("Scrape stale or Prometheus unreachable", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("OK", data));
    }

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        return context.Response.WriteAsJsonAsync(result);
    }
}
