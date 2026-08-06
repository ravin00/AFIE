using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Publishers;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Services;

public class StateVectorEmitterServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void Ctor_InvalidEmitIntervalSeconds_Throws(int seconds)
    {
        var options = Options.Create(new FeatureEngineeringOptions
        {
            WindowCapacity = 10,
            EmitIntervalSeconds = seconds
        });
        var store = new WindowStore(options);
        var builder = BuildersForTest.MinimalBuilder(options, out var publisher);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StateVectorEmitterService(
                store,
                builder,
                publisher,
                options,
                NullLogger<StateVectorEmitterService>.Instance));
    }

    private static class BuildersForTest
    {
        public static StateVectorBuilder MinimalBuilder(
            IOptions<FeatureEngineeringOptions> options,
            out IStateVectorPublisher publisher)
        {
            IFeatureGroup[] groups =
            {
                new CpuFeatures(),
                new MemoryFeatures(),
                new AppSignalFeatures(),
                new NodePressureFeatures(),
                new CostFeatures(),
                new TemporalFeatures(),
                new DeploymentFeatures(),
                new ActionHistoryFeatures(),
            };
            var actions = new ActionHistoryStore();
            publisher = new NoopPublisher();
            return new StateVectorBuilder(groups, options, actions);
        }
    }

    private sealed class NoopPublisher : IStateVectorPublisher
    {
        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PublishAsync(StateVector vector, CancellationToken ct = default) => Task.CompletedTask;
    }
}
