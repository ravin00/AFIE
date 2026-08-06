namespace AFIE.FeatureEngineering.Features;

public sealed class DeploymentFeatures : IFeatureGroup
{
    public int StartDim => 35;
    public int Length => 3;

    public void Compute(FeatureContext ctx, Span<float> dest)
    {
        // Placeholders until Phase 6 adds a Kubernetes informer.
        dest[0] = 0.1f;  // replica count / 10 (assumes 1 replica)
        dest[1] = 0f;    // HPA target utilisation
        dest[2] = 0f;    // rolling-update-in-progress flag
    }
}
