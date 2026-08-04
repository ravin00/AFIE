using System.Text.Json;
using AFIE.Contracts;
using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Consumers;

public sealed class LocalJsonlTailConsumer : BackgroundService, IMetricEventConsumer
{
    private const int OffsetFlushEveryN = 50;
    private static readonly TimeSpan RolloverIdleThreshold = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly WindowStore _store;
    private readonly FeatureEngineeringHealthState _health;
    private readonly FeatureEngineeringOptions _options;
    private readonly ILogger<LocalJsonlTailConsumer> _logger;
    private OffsetState _offset = new("", 0, 0);
    private int _sinceLastFlush;

    public LocalJsonlTailConsumer(
        WindowStore store,
        FeatureEngineeringHealthState health,
        IOptions<FeatureEngineeringOptions> options,
        ILogger<LocalJsonlTailConsumer> logger)
    {
        _store = store;
        _health = health;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_options.InputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_options.OffsetStatePath) ?? ".");
        _offset = LoadOffset();

        _logger.LogInformation("Tail consumer starting, input={Input}, poll={Poll}ms",
            _options.InputPath, _options.PollingIntervalMs);

        var delay = TimeSpan.FromMilliseconds(_options.PollingIntervalMs);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
                _health.SourceFileReachable = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Tail poll failed");
                _health.SourceFileReachable = false;
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        FlushOffset();
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var todayFile = $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        var todayPath = Path.Combine(_options.InputPath, todayFile);

        var activeFile = string.IsNullOrEmpty(_offset.File) ? todayFile : _offset.File;
        var activePath = Path.Combine(_options.InputPath, activeFile);

        if (activeFile != todayFile && File.Exists(todayPath) && ShouldRollover(activePath))
        {
            FlushOffset();
            _offset = new OffsetState(todayFile, 0, 0);
            activePath = todayPath;
        }

        if (!File.Exists(activePath)) return;

        var info = new FileInfo(activePath);
        if (info.Length < _offset.Offset) _offset = _offset with { Offset = 0, Size = 0 };
        if (info.Length == _offset.Offset) return;

        await using var fs = new FileStream(activePath, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        fs.Seek(_offset.Offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var evt = JsonSerializer.Deserialize<MetricEvent>(line, JsonOptions);
                if (evt is null) continue;
                _store.Add(evt);
                _health.LastEventConsumedTime = DateTimeOffset.UtcNow;
                _health.EventsConsumedTotal++;
                if (++_sinceLastFlush >= OffsetFlushEveryN)
                {
                    _offset = new OffsetState(Path.GetFileName(activePath), fs.Position, info.Length);
                    FlushOffset();
                    _sinceLastFlush = 0;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed JSONL line");
            }
        }

        _offset = new OffsetState(Path.GetFileName(activePath), fs.Position, info.Length);
        _health.SourceFileOffset = _offset.Offset;
        FlushOffset();
    }

    private static bool ShouldRollover(string activePath)
    {
        if (!File.Exists(activePath)) return true;
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(activePath) > RolloverIdleThreshold;
    }

    private OffsetState LoadOffset()
    {
        try
        {
            if (!File.Exists(_options.OffsetStatePath)) return new OffsetState("", 0, 0);
            var json = File.ReadAllText(_options.OffsetStatePath);
            return JsonSerializer.Deserialize<OffsetState>(json) ?? new OffsetState("", 0, 0);
        }
        catch
        {
            return new OffsetState("", 0, 0);
        }
    }

    private void FlushOffset()
    {
        try
        {
            File.WriteAllText(_options.OffsetStatePath, JsonSerializer.Serialize(_offset));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush offset");
        }
    }

    private record OffsetState(string File, long Offset, long Size);
}