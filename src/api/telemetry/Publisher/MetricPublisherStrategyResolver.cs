using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class MetricPublisherStrategyResolver
{
    private readonly Dictionary<string, Func<IMetricPublisher>> _strategies;
    private readonly string _mode;

    public MetricPublisherStrategyResolver(
        Lazy<LocalFilePublisher> local,
        Lazy<EventHubPublisher> eventHub,
        IOptions<TelemetryOptions> options)
    {
        _strategies = new(StringComparer.OrdinalIgnoreCase)
        {
            [LocalFilePublisher.ModeName] = () => local.Value,
            [EventHubPublisher.ModeName] = () => eventHub.Value,
        };
        _mode = options.Value.OutputMode?.Trim() ?? string.Empty;
    }

    public IMetricPublisher Resolve()
    {
        if (_strategies.TryGetValue(_mode, out var factory))
            return factory();

        throw new InvalidOperationException(
            $"No metric publisher registered for OutputMode '{_mode}'. " +
            $"Available: {string.Join(", ", _strategies.Keys)}");
    }
}
