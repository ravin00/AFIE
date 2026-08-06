using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Publishers;

public sealed class AzureMlFeatureStorePublisher : IStateVectorPublisher
{
    private readonly ILogger<AzureMlFeatureStorePublisher> _logger;

    public AzureMlFeatureStorePublisher(ILogger<AzureMlFeatureStorePublisher> logger) => _logger = logger;

    public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public Task PublishAsync(StateVector vector, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "AzureMlFeatureStorePublisher is a Phase-8 stub. Set FeatureEngineering:PublisherMode=postgres.");
        return Task.CompletedTask;
        // TODO(phase-8): implement via Azure ML Feature Store REST API.
    }
}