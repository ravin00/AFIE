using AFIE.Telemetry.Models;


namespace AFIE.Telemetry.Publishers;

public interface IMetricPublisher
{
    Task PublishAsync(IReadOnlyList<MetricEvent> events, CancellationToken ct = default);
}