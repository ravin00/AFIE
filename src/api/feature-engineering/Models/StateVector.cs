namespace AFIE.FeatureEngineering.Models;

public record StateVector(
    string WorkloadName,
    string Namespace,
    DateTimeOffset Timestamp,
    float[] Values
)
{
    public const int Dimensions = 47;
    public float[] Values { get; init; } = ValidateValues(Values);

    private static float[] ValidateValues(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != Dimensions)
            throw new ArgumentException(
                $"Values length must be {Dimensions}, got {values.Length}.",
                nameof(values));
        return values;
    }
}
