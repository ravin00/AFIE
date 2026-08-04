using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Health;

public sealed class FeatureEngineeringHealthCheck : IHealthCheck
{
    private readonly FeatureEngineeringHealthState _state;
    private readonly WindowStore _store;
    private readonly FeatureEngineeringOptions _options;

    public FeatureEngineeringHealthCheck(
        FeatureEngineeringHealthState state,
        WindowStore store,
        IOptions<FeatureEngineeringOptions> options)
    {
        _state = state;
        _store = store;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct = default)
    {
        var data = new Dictionary<string, object>
        {
            ["lastEventConsumedTime"] = _state.LastEventConsumedTime?.ToString("o") ?? "never",
            ["eventsConsumedTotal"] = _state.EventsConsumedTotal,
            ["stateVectorsWrittenTotal"] = _state.StateVectorsWrittenTotal,
            ["sourceFileReachable"] = _state.SourceFileReachable,
            ["sourceFileOffset"] = _state.SourceFileOffset,
            ["postgresReachable"] = _state.PostgresReachable,
            ["workloadsTracked"] = _store.WorkloadCount
        };

        if (_state.LastEventConsumedTime is null)
            return Task.FromResult(HealthCheckResult.Degraded("No events consumed yet", data: data));

        var staleness = DateTimeOffset.UtcNow - _state.LastEventConsumedTime.Value;
        var threshold = TimeSpan.FromSeconds(_options.EventStalenessThresholdSeconds);

        if (staleness > threshold || !_state.SourceFileReachable || !_state.PostgresReachable)
            return Task.FromResult(HealthCheckResult.Degraded("Event stream stale or dependency unreachable", data: data));

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
