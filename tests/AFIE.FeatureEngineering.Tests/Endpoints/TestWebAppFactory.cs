using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Publishers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFIE.FeatureEngineering.Tests.Endpoints;

/// Program.cs requires a real Postgres at startup. Endpoint tests only exercise
/// the HTTP surface, so we satisfy the connection-string check with a dummy and
/// swap the publisher for a no-op that never opens a connection.
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable(
            "FeatureEngineering__PostgresConnectionString",
            "Host=127.0.0.1;Port=5432;Database=x;Username=x;Password=x");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStateVectorPublisher>();
            services.AddSingleton<IStateVectorPublisher, NoopPublisher>();
        });
    }

    private sealed class NoopPublisher : IStateVectorPublisher
    {
        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PublishAsync(StateVector vector, CancellationToken ct = default) => Task.CompletedTask;
    }
}
