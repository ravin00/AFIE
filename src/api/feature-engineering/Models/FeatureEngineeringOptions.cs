namespace AFIE.FeatureEngineering.Models;

public class FeatureEngineeringOptions
{
    public int WindowCapacity { get; set; } = 240;
    public int EventStalenessThresholdSeconds { get; set; } = 60;
}