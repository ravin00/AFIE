namespace AFIE.FeatureEngineering.Models;

/// <summary>Raw cloud pricing inputs for one workload (feature dims 47–54).</summary>
public class CloudPricingProfile
{
    /// <summary>Spot price / on-demand price, 0–1.</summary>
    public double SpotPriceRatio { get; set; } = 0.3;
    /// <summary>Fraction of compute covered by reservations, 0–1.</summary>
    public double ReservationCoverage { get; set; }
    public double StorageAccessFrequencyPerHour { get; set; } = 1.0;
    /// <summary>Current region price vs cheapest equivalent region, 0–1.</summary>
    public double RegionPriceIndex { get; set; } = 0.5;
    public bool CommitmentDiscountAvailable { get; set; } = true;
    public bool IsStateful { get; set; }
    /// <summary>Best same-continent alternative region price index, 0–1.</summary>
    public double CheaperRegionPriceIndex { get; set; } = 0.5;
    public double CrossRegionP99BaselineMs { get; set; }
}
