using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Publishers;
using Microsoft.Extensions.Options;

namespace AFIE.FeatureEngineering.Services;

public sealed class StateVectorEmitterService : BackgroundService
{
    private readonly WindowStore _store;
    private readonly StateVectorBuilder _builder;
    private readonly IStateVectorPublisher _publisher;
    private readonly FeatureEngineeringOptions _options;
    private readonly ILogger<StateVectorEmitterService> _logger;

    public StateVectorEmitterService(
        WindowStore store,
        StateVectorBuilder builder,
        IStateVectorPublisher publisher,
        IOptions<FeatureEngineeringOptions> options,
        ILogger<StateVectorEmitterService> logger)
    {
        _store = store;
        _builder = builder;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;

        if (_options.EmitIntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.EmitIntervalSeconds,
                $"{nameof(FeatureEngineeringOptions.EmitIntervalSeconds)} must be greater than zero.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.EmitIntervalSeconds);
        _logger.LogInformation("Emitter starting, interval={Interval}s", _options.EmitIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            var now = DateTimeOffset.UtcNow;
            var workloads = _store.Workloads;
            var emitted = 0;

            foreach (var workload in workloads)
            {
                var samples = _store.Snapshot(workload);
                if (samples is null || samples.Length == 0) continue;

                try
                {
                    var vector = _builder.Build(workload, samples, now);
                    await _publisher.PublishAsync(vector, stoppingToken);
                    emitted++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Emit failed for {Workload}", workload);
                }
            }

            if (emitted > 0)
                _logger.LogInformation("Emit cycle: {Emitted}/{Total} workloads", emitted, workloads.Count);
        }
    }
}
