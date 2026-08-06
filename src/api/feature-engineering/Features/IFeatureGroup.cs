using AFIE.Contracts;
using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Features;

public interface IFeatureGroup
{
    int StartDim { get; }
    int Length { get; }

    void Compute(FeatureContext ctx, Span<float> destination);
}

public sealed record FeatureContext(
    string WorkloadName,
    MetricEvent[] Samples,
    DateTimeOffset Now,
    FeatureEngineeringOptions Options,
    ActionRecord[] ActionHistory
);