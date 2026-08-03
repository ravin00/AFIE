using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AFIE.FeatureEngineering.Health;

public sealed class FeatureEngineeringHealthCheck : IHealthCheck
{
    private readonly FeatureEngineeringHealthState _state;
    private readonly WindowStore _store;

    public FeatureEngineeringHealthCheck(FeatureEngineeringHealthState state, WindowStore store)
    {
        _state = state;
        _store = store;
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
