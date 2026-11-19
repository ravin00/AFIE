using System.Text.RegularExpressions;
using AFIE.Contracts;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Features;

public class CloudPricingFeaturesTests
{
    private static float[] Compute(FeatureEngineeringOptions opts, string workload = "redis")
    {
        var buf = new float[8];
        new CloudPricingFeatures().Compute(
            new FeatureContext(workload, Array.Empty<MetricEvent>(), DateTimeOffset.UtcNow, opts, Array.Empty<ActionRecord>()),
            buf);
        return buf;
    }

    [Fact]
    public void UsesPerWorkloadProfile_OverDefault()
    {
        var opts = new FeatureEngineeringOptions();
        opts.CloudPricing["redis"] = new CloudPricingProfile { IsStateful = true, SpotPriceRatio = 0.25, CrossRegionP99BaselineMs = 80 };

        var v = Compute(opts);

        Assert.Equal(0.25f, v[0]);
        Assert.Equal(1f, v[5]);              // dim 52: stateful, read by heuristic.py STATEFUL_DIM
        Assert.InRange(v[7], 0.0799f, 0.0801f);
        Assert.Equal(0f, Compute(opts, "web-api")[5]); // falls back to default (not stateful)
    }

    [Fact]
    public void ClampsOutOfRangeInputs()
    {
        var opts = new FeatureEngineeringOptions
        {
            DefaultCloudPricing = new CloudPricingProfile
            {
                SpotPriceRatio = 3, ReservationCoverage = -1, StorageAccessFrequencyPerHour = -5,
                RegionPriceIndex = 9, CheaperRegionPriceIndex = -2, CrossRegionP99BaselineMs = 99_999,
            }
        };

        var v = Compute(opts);

        Assert.All(v, x => Assert.InRange(x, 0f, 1f));
        Assert.Equal(1f, v[0]);
        Assert.Equal(0f, v[1]);
        Assert.Equal(0f, v[2]);
        Assert.Equal(1f, v[7]);
    }

    // Contract: C# vector width must equal the Python agent's observation width.
    [Fact]
    public void Dimensions_MatchPythonFeatureNames()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AFIE.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var py = File.ReadAllText(Path.Combine(dir!.FullName, "src/ml/afie_ml/feature_names.py"));
        var dim = int.Parse(Regex.Match(py, @"STATE_VECTOR_DIM:\s*Final\[int\]\s*=\s*(\d+)").Groups[1].Value);

        Assert.Equal(StateVector.Dimensions, dim);
        Assert.Contains("\"Workload is stateful\"", py);
    }
}
