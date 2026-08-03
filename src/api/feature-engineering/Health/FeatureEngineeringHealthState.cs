namespace AFIE.FeatureEngineering.Health;

public sealed class FeatureEngineeringHealthState
{
    public DateTimeOffset? LastEventConsumedTime { get; set; }
    public long EventsConsumedTotal { get; set; }
    public long StateVectorsWrittenTotal { get; set; }
    public bool SourceFileReachable { get; set; }
    public long SourceFileOffset { get; set; }
    public bool PostgresReachable { get; set; }
}
