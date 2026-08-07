using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using Dapper;
using Npgsql;

namespace AFIE.FeatureEngineering.Publishers;

public sealed class PostgresStateWriter : IStateVectorPublisher
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS state_vectors (
          id         BIGSERIAL PRIMARY KEY,
          workload   TEXT        NOT NULL,
          namespace  TEXT        NOT NULL,
          ts         TIMESTAMPTZ NOT NULL,
          vector     BYTEA       NOT NULL,
          CHECK (octet_length(vector) = 188)
        );
        CREATE INDEX IF NOT EXISTS idx_sv_workload_ts ON state_vectors(workload, ts DESC);
    """;

    private const string InsertSql = """
        INSERT INTO state_vectors(workload, namespace, ts, vector)
        VALUES (@Workload, @Namespace, @Ts, @Vector);
    """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly FeatureEngineeringHealthState _health;
    private readonly ILogger<PostgresStateWriter> _logger;

    public PostgresStateWriter(
        NpgsqlDataSource dataSource,
        FeatureEngineeringHealthState health,
        ILogger<PostgresStateWriter> logger)
    {
        _dataSource = dataSource;
        _health = health;
        _logger = logger;
    }

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(SchemaSql);
            _health.PostgresReachable = true;
            _logger.LogInformation("Postgres schema ready");
        }
        catch (Exception ex)
        {
            _health.PostgresReachable = false;
            _logger.LogError(ex, "Postgres schema init failed");
            throw;
        }
    }

    public async Task PublishAsync(StateVector vector, CancellationToken ct = default)
    {
        var bytes = new byte[vector.Values.Length * sizeof(float)];
        Buffer.BlockCopy(vector.Values, 0, bytes, 0, bytes.Length);

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(InsertSql, new
            {
                Workload = vector.WorkloadName,
                Namespace = vector.Namespace,
                Ts = vector.Timestamp,
                Vector = bytes
            });
            _health.StateVectorsWrittenTotal++;
            _health.PostgresReachable = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _health.PostgresReachable = false;
            _logger.LogError(ex, "Postgres insert failed for {Workload}", vector.WorkloadName);
            throw;
        }
    }
}