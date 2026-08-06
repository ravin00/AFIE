using AFIE.Contracts;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Features;

public sealed class StateVectorBuilder
{
    private readonly IFeatureGroup[] _groups;
    private readonly FeatureEngineeringOptions _options;
    private readonly ActionHistoryStore _actions;

    public StateVectorBuilder(
        IEnumerable<IFeatureGroup> groups,
        IOptions<FeatureEngineeringOptions> options,
        ActionHistoryStore actions)
    {
        _groups = groups.OrderBy(g => g.StartDim).ToArray();
        _options = options.Value;
        _actions = actions;

        var expected = 0;
        foreach (var g in _groups)
        {
            if (g.StartDim != expected)
                throw new InvalidOperationException(
                    $"Feature group {g.GetType().Name} expected StartDim={expected}, got {g.StartDim}");
            expected += g.Length;
        }
        if (expected != StateVector.Dimensions)
            throw new InvalidOperationException(
                $"Feature groups sum to {expected} dims, expected {StateVector.Dimensions}");
    }

    public StateVector Build(string workloadName, MetricEvent[] samples, DateTimeOffset now)
    {
        var values = new float[StateVector.Dimensions];
        var ctx = new FeatureContext(workloadName, samples, now, _options, _actions.Recent(workloadName));

        foreach (var group in _groups)
            group.Compute(ctx, values.AsSpan(group.StartDim, group.Length));

        for (var i = 0; i < values.Length; i++)
        {
            var v = values[i];
            if (float.IsNaN(v) || float.IsInfinity(v)) values[i] = 0f;
            else values[i] = Math.Clamp(v, -2f, 2f);
        }

        var ns = samples.Length > 0 ? samples[^1].Namespace : "unknown";
        return new StateVector(workloadName, ns, now, values);
    }
}