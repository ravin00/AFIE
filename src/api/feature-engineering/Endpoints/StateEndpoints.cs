using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Services;

namespace AFIE.FeatureEngineering.Endpoints;

public static class StateEndpoints
{
    public static void MapStateEndpoints(this WebApplication app)
    {
        app.MapGet("/state/{workloadName}", (
            string workloadName,
            WindowStore store,
            StateVectorBuilder builder) =>
        {
            var samples = store.Snapshot(workloadName);
            if (samples is null)
                return Results.NotFound(new { error = "workload not tracked", workload = workloadName });

            var vector = builder.Build(workloadName, samples, DateTimeOffset.UtcNow);
            return Results.Ok(vector.Values);
        });

        app.MapGet("/state/{workloadName}/latest", (
            string workloadName,
            WindowStore store,
            StateVectorBuilder builder) =>
        {
            var samples = store.Snapshot(workloadName);
            if (samples is null)
                return Results.NotFound(new { error = "workload not tracked", workload = workloadName });

            var vector = builder.Build(workloadName, samples, DateTimeOffset.UtcNow);
            return Results.Ok(new { timestamp = vector.Timestamp, values = vector.Values });
        });
    }
}