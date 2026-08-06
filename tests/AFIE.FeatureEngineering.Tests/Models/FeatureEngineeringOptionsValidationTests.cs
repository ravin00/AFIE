using AFIE.FeatureEngineering.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Models;

public class FeatureEngineeringOptionsValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void EmitIntervalSeconds_NonPositive_FailsValidation(int seconds)
    {
        var provider = BuildProvider(seconds);
        var options = provider.GetRequiredService<IOptions<FeatureEngineeringOptions>>();

        var ex = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains(nameof(FeatureEngineeringOptions.EmitIntervalSeconds), ex.Message);
    }

    [Fact]
    public void EmitIntervalSeconds_Positive_PassesValidation()
    {
        var provider = BuildProvider(60);
        var options = provider.GetRequiredService<IOptions<FeatureEngineeringOptions>>();

        Assert.Equal(60, options.Value.EmitIntervalSeconds);
    }

    private static ServiceProvider BuildProvider(int emitIntervalSeconds)
    {
        var services = new ServiceCollection();
        services.AddOptions<FeatureEngineeringOptions>()
            .Configure(o => o.EmitIntervalSeconds = emitIntervalSeconds)
            .ValidateDataAnnotations();
        return services.BuildServiceProvider();
    }
}
