namespace AFIE.FeatureEngineering.Features;

public sealed class ActionHistoryFeatures : IFeatureGroup
{
    public int StartDim => 38;
    public int Length => 9;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        dest.Clear();
        var history = ctx.ActionHistory;
        var take = Math.Min(history.Length, 3);
        for (var i = 0; i < take; i++)
        {
            var record = history[history.Length - 1 - i];  // most recent first
            var minutesScaled = Math.Clamp((ctx.Now - record.Timestamp).TotalMinutes / 60.0, 0.0,1.0);
            dest[i * 3 + 0] = (float)Math.Clamp(record.CostDelta, -1.0, 1.0);
            dest[i * 3 + 1] = (float)Math.Clamp(record.SloDelta, -1.0, 1.0);
            dest[i * 3 + 2] = (float)minutesScaled;
        }
    }
}