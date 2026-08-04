namespace AFIE.FeatureEngineering.Models;

public class FeatureEngineeringOptions
{
    public int WindowCapacity { get; set; } = 240;
    public int EventStalenessThresholdSeconds { get; set; } = 60;
    public string ConsumerMode { get; set; } = "local";
    public string InputPath { get; set; } = "experiments/results";
    public string OffsetStatePath { get; set; } = "experiments/state/fe_consumer_offset.json";
    public int PollingIntervalMs { get; set; } = 500;
}