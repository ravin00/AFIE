using AFIE.FeatureEngineering.Models;

namespace AFIE.FeatureEngineering.Publishers;

public interface IStateVectorPublisher
{
    Task EnsureReadyAsync(CancellationToken ct);
    Task PublishAsync(StateVector vector, CancellationToken ct = default);
}