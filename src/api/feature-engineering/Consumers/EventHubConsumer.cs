using AFIE.FeatureEngineering.Models;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Consumers;

public sealed class EventHubConsumer : BackgroundService, IMetricEventConsumer
{
    private readonly EventHubOptions _options;
    private readonly ILogger<EventHubConsumer> _logger;

    public EventHubConsumer(IOptions<EventHubOptions> options, ILogger<EventHubConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning(
            "EventHubConsumer is a Phase-8 stub. Namespace={Ns}, Hub={Hub}. " +
            "Set FeatureEngineering:ConsumerMode=local for now.",
            _options.FullyQualifiedNamespace, _options.EventHubName);
        return Task.CompletedTask;
    }
}