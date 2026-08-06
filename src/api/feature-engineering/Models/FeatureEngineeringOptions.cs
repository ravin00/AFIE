using System.ComponentModel.DataAnnotations;

namespace AFIE.FeatureEngineering.Models;

public class FeatureEngineeringOptions
{
    public int WindowCapacity { get; set; } = 240;
    public int EventStalenessThresholdSeconds { get; set; } = 60;
    public string ConsumerMode { get; set; } = "local";
    public string InputPath { get; set; } = "experiments/results";
    public string OffsetStatePath { get; set; } = "experiments/state/fe_consumer_offset.json";
    public int PollingIntervalMs { get; set; } = 500;

    public double ConfiguredBudgetUsdPerHour { get; set; } = 10.0;
    public double CpuCostPerCoreHourUsd { get; set; } = 0.031;
    public double MemCostPerGiBHourUsd { get; set; } = 0.004;

    public string PublisherMode { get; set; } = "postgres";
    public string PostgresConnectionString { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "EmitIntervalSeconds must be greater than zero.")]
    public int EmitIntervalSeconds { get; set; } = 60;
}