using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class EventHubPublisher : IMetricPublisher, IAsyncDisposable
{
    private readonly EventHubProducerClient _producer;
    private readonly ILogger<EventHubPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EventHubPublisher(IOptions<EventHubOptions> options, ILogger<EventHubPublisher> logger)
    {
        _logger = logger;
        _producer = new EventHubProducerClient(
            options.Value.FullyQualifiedNamespace,
            options.Value.EventHubName,
            new DefaultAzureCredential());
    }

    public async Task PublishAsync(IReadOnlyList<MetricEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return;

        var grouped = events.GroupBy(e => e.WorkloadName);

        foreach (var group in grouped)
        {
            var batchOptions = new CreateBatchOptions { PartitionKey = group.Key };
            using var batch = await _producer.CreateBatchAsync(batchOptions, ct);

            foreach (var evt in group)
            {
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                var eventData = new EventData(Encoding.UTF8.GetBytes(json));

                if (!batch.TryAdd(eventData))
                {
                    _logger.LogWarning("Event too large for batch, skipping: {Workload}", evt.WorkloadName);
                }
            }

            await _producer.SendAsync(batch, ct);
        }

        _logger.LogInformation("Published {Count} events to Event Hub", events.Count);
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync();
    }
}