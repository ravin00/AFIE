namespace AFIE.FeatureEngineering.Models;

public record ActionRecord(
    string WorkloadName,
    DateTimeOffset Timestamp,
    double CostDelta,
    double SloDelta
);
