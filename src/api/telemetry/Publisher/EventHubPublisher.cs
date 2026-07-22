using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class EventHubPublisher : MetricPublisherBase, IAsyncDisposable
{
    public const string ModeName = "eventhub";

    private readonly EventHubProducerClient _producer;
    private readonly ILogger<EventHubPublisher> _logger;

    public EventHubPublisher(IOptions<EventHubOptions> options, ILogger<EventHubPublisher> logger)
        : base(logger)
    {
        _logger = logger;
        _producer = new EventHubProducerClient(
            options.Value.FullyQualifiedNamespace,
            options.Value.EventHubName,
            new DefaultAzureCredential());
    }

    public override string Mode => ModeName;

    protected override async Task<string> PublishBatchAsync(
        IReadOnlyList<MetricEvent> events, CancellationToken ct)
    {
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

        return _producer.EventHubName;
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync();
    }
}
