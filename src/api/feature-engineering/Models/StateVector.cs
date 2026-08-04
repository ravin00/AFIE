namespace AFIE.FeatureEngineering.Models;

public record StateVector(
    string WorkloadName,
    string Namespace,
    DateTimeOffset TimeStamp,
    float[] Values
)
{
    public const int Dimensions = 47;
}