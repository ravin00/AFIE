namespace AFIE.FeatureEngineering.Features;

public sealed class TemporalFeatures : IFeatureGroup
{
    public int StartDim => 30;
    public int Length => 5;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        var now = ctx.Now.UtcDateTime;

        var hourFrac = now.Hour + now.Minute / 60.0 + now.Second / 3600.0;
        var hourAngle = 2 * Math.PI * hourFrac / 24.0;
        dest[0] = (float)Math.Sin(hourAngle);
        dest[1] = (float)Math.Cos(hourAngle);

        var dowFrac = (int)now.DayOfWeek + hourFrac / 24.0;
        var dowAngle = 2 * Math.PI * dowFrac / 7.0;
        dest[2] = (float)Math.Sin(dowAngle);
        dest[3] = (float)Math.Cos(dowAngle);

        var age = ctx.Samples.Length > 0
            ? (ctx.Now - ctx.Samples[0].Timestamp).TotalDays
            : 0;
        dest[4] = (float)Math.Min(age / 365.0, 1.0);
    }
}
