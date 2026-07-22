using System.Text.Json;
using AFIE.Telemetry.Models;
using Microsoft.Extensions.Options;

namespace AFIE.Telemetry.Publishers;

public class LocalFilePublisher : MetricPublisherBase
{
    public const string ModeName = "local";

    private readonly string _outputPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalFilePublisher(IOptions<TelemetryOptions> options, ILogger<LocalFilePublisher> logger)
        : base(logger)
    {
        _outputPath = options.Value.OutputPath;
    }

    public override string Mode => ModeName;

    protected override async Task<string> PublishBatchAsync(
        IReadOnlyList<MetricEvent> events, CancellationToken ct)
    {
        var fileName = $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        var filePath = Path.Combine(_outputPath, fileName);

        await _lock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_outputPath);

            var lines = events.Select(e => JsonSerializer.Serialize(e, JsonOptions));
            await File.AppendAllLinesAsync(filePath, lines, ct);
        }
        finally
        {
            _lock.Release();
        }

        return filePath;
    }
}
