using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Publishers;

public sealed class AzureMlFeatureStorePublisher : IStateVectorPublisher
{
    private readonly ILogger<AzureMlFeatureStorePublisher> _logger;

    public AzureMlFeatureStorePublisher(ILogger<AzureMlFeatureStorePublisher> logger) => _logger = logger;

    public Task EnsureReadyAsync(CancellationToken ct) =>
        throw new NotImplementedException(
            "AzureMlFeatureStorePublisher is a Phase-8 stub. " +
            "Set FeatureEngineering:PublisherMode=postgres until Azure ML Feature Store persistence is implemented.");

    public Task PublishAsync(StateVector vector, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "AzureMlFeatureStorePublisher.PublishAsync is not implemented. " +
            "Set FeatureEngineering:PublisherMode=postgres until Azure ML Feature Store persistence is implemented.");
    // TODO(phase-8): implement via Azure ML Feature Store REST API.
}