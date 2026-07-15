using System.Text.Json;
using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class LocalFilePublisher : IMetricPublisher
{
    private readonly string _outputPath;
    private readonly ILogger<LocalFilePublisher> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalFilePublisher(IOptions<TelemetryOptions> options, ILogger<LocalFilePublisher> logger)
    {
        _outputPath = options.Value.OutputPath;
        _logger = logger;
    }

    public async Task PublishAsync(IReadOnlyList<MetricEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return;

        var fileName = $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        var filePath = Path.Combine(_outputPath, fileName);

        await _lock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_outputPath);

            var lines = events.Select(e => JsonSerializer.Serialize(e, JsonOptions));
            await File.AppendAllLinesAsync(filePath, lines, ct);

            _logger.LogInformation("Published {Count} events to {File}", events.Count, filePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}