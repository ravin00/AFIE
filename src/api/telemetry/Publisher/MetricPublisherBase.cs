using System.Text.Json;
using AFIE.Contracts;

namespace AFIE.Telemetry.Publishers;

public abstract class MetricPublisherBase : IMetricPublisher
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger;

    protected MetricPublisherBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string Mode { get; }

    public async Task PublishAsync(IReadOnlyList<MetricEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return;

        var destination = await PublishBatchAsync(events, ct);

        _logger.LogInformation("Published {Count} events via {Mode} to {Destination}",
            events.Count, Mode, destination);
    }

    protected abstract Task<string> PublishBatchAsync(
        IReadOnlyList<MetricEvent> events, CancellationToken ct);
}
