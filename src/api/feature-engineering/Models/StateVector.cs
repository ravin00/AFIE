namespace AFIE.FeatureEngineering.Models;

public record StateVector(
    string WorkloadName,
    string Namespace,
    DateTimeOffset Timestamp,
    float[] Values
)
{
    public const int Dimensions = 47;
    private readonly float[] _values = ValidateValues(Values);
    public float [] Values
    {
        get => _values;
        init => _values = ValidateValues(value);
    }

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
