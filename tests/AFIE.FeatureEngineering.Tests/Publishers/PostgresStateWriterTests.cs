using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Publishers;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Publishers;

public class PostgresStateWriterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("afie")
        .WithUsername("afie")
        .WithPassword("afie")
        .Build();

    private NpgsqlDataSource _dataSource = null!;
    private FeatureEngineeringHealthState _health = null!;
    private PostgresStateWriter _writer = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _dataSource = new NpgsqlDataSourceBuilder(_pg.GetConnectionString()).Build();
        _health = new FeatureEngineeringHealthState();
        _writer = new PostgresStateWriter(_dataSource, _health, NullLogger<PostgresStateWriter>.Instance);
        await _writer.EnsureReadyAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private static StateVector SampleVector(string workload = "nginx")
    {
        var values = new float[StateVector.Dimensions];
        for (var i = 0; i < StateVector.Dimensions; i++) values[i] = i * 0.01f;
        return new StateVector(workload, "afie-system", DateTimeOffset.UtcNow, values);
    }

    [Fact]
    public async Task EnsureReadyAsync_CreatesSchema()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'state_vectors')");
        Assert.True(exists);
        Assert.True(_health.PostgresReachable);
    }

    [Fact]
    public async Task PublishAsync_RoundTrip_VectorIs220Bytes()
    {
        await _writer.PublishAsync(SampleVector());

        await using var conn = await _dataSource.OpenConnectionAsync();
        var len = await conn.ExecuteScalarAsync<int>(
            "SELECT octet_length(vector) FROM state_vectors ORDER BY id DESC LIMIT 1");
        Assert.Equal(220, len);
    }

    [Fact]
    public async Task EnsureReadyAsync_Migrates47DimTable_KeepingOldRows()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("""
            DROP TABLE state_vectors;
            CREATE TABLE state_vectors (
              id BIGSERIAL PRIMARY KEY, workload TEXT NOT NULL, namespace TEXT NOT NULL,
              ts TIMESTAMPTZ NOT NULL, vector BYTEA NOT NULL, CHECK (octet_length(vector) = 188));
            INSERT INTO state_vectors(workload, namespace, ts, vector)
              VALUES ('old', 'ns', now(), decode(repeat('00', 188), 'hex'));
            """);

        await _writer.EnsureReadyAsync(CancellationToken.None);
        await _writer.EnsureReadyAsync(CancellationToken.None); // idempotent
        await _writer.PublishAsync(SampleVector());

        var lens = (await conn.QueryAsync<int>("SELECT octet_length(vector) FROM state_vectors ORDER BY id")).ToArray();
        Assert.Equal(new[] { 188, 220 }, lens);
    }

    [Fact]
    public async Task PublishAsync_UpdatesHealthState()
    {
        var before = _health.StateVectorsWrittenTotal;
        await _writer.PublishAsync(SampleVector());
        await _writer.PublishAsync(SampleVector("redis"));
        Assert.Equal(before + 2, _health.StateVectorsWrittenTotal);
        Assert.True(_health.PostgresReachable);
    }

    [Fact]
    public async Task PublishAsync_ConnectionFailure_ThrowsAndMarksUnreachable()
    {
        await _pg.StopAsync();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => _writer.PublishAsync(SampleVector()));
            Assert.False(_health.PostgresReachable);
        }
        finally
        {
            await _pg.StartAsync();
        }
    }
}
