using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class MetricPublisherStrategyResolver
{
    private readonly Dictionary<string, Func<IMetricPublisher>> _strategies;
    private readonly string _mode;

    public MetricPublisherStrategyResolver(
        IServiceProvider services,
        IOptions<TelemetryOptions> options)
    {
        _strategies = new(StringComparer.OrdinalIgnoreCase)
        {
            [LocalFilePublisher.ModeName] = services.GetRequiredService<LocalFilePublisher>,
            [EventHubPublisher.ModeName] = services.GetRequiredService<EventHubPublisher>,
        };
        _mode = options.Value.OutputMode;
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
