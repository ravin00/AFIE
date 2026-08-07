using AFIE.FeatureEngineering.Consumers;
using AFIE.FeatureEngineering.Endpoints;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Publishers;
using AFIE.FeatureEngineering.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var section = builder.Configuration.GetSection("FeatureEngineering");
builder.Services.AddOptions<FeatureEngineeringOptions>()
    .Bind(section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<EventHubOptions>(builder.Configuration.GetSection("EventHub"));

var postgresConnectionString = section["PostgresConnectionString"];
if (string.IsNullOrWhiteSpace(postgresConnectionString))
    throw new InvalidOperationException(
        "FeatureEngineering:PostgresConnectionString is not configured. " +
        "For local dev, set it via User Secrets: " +
        "`dotnet user-secrets set \"FeatureEngineering:PostgresConnectionString\" \"...\" " +
        "--project src/api/feature-engineering`. " +
        "For production, source it from a Kubernetes Secret via the " +
        "FeatureEngineering__PostgresConnectionString environment variable.");

var dataSource = new NpgsqlDataSourceBuilder(postgresConnectionString).Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddSingleton<FeatureEngineeringHealthState>();
builder.Services.AddSingleton<ActionHistoryStore>();
builder.Services.AddSingleton<WindowStore>();

builder.Services.AddSingleton<IFeatureGroup, CpuFeatures>();
builder.Services.AddSingleton<IFeatureGroup, MemoryFeatures>();
builder.Services.AddSingleton<IFeatureGroup, AppSignalFeatures>();
builder.Services.AddSingleton<IFeatureGroup, NodePressureFeatures>();
builder.Services.AddSingleton<IFeatureGroup, CostFeatures>();
builder.Services.AddSingleton<IFeatureGroup, TemporalFeatures>();
builder.Services.AddSingleton<IFeatureGroup, DeploymentFeatures>();
builder.Services.AddSingleton<IFeatureGroup, ActionHistoryFeatures>();
builder.Services.AddSingleton<StateVectorBuilder>();

var publisherMode = section["PublisherMode"] ?? "postgres";
switch (publisherMode)
{
    case "postgres":
        builder.Services.AddSingleton<IStateVectorPublisher, PostgresStateWriter>();
        break;
    case "azureml":
        throw new InvalidOperationException(
            "FeatureEngineering:PublisherMode=azureml is not yet implemented. " +
            "Set PublisherMode=postgres until AzureMlFeatureStorePublisher is completed (Phase 8).");
    default:
        throw new InvalidOperationException(
            $"FeatureEngineering:PublisherMode='{publisherMode}' is not supported. " +
            "Supported values: 'postgres'.");
}

var consumerMode = section["ConsumerMode"] ?? "local";
if (consumerMode == "eventhub")
    builder.Services.AddHostedService<EventHubConsumer>();
else
    builder.Services.AddHostedService<LocalJsonlTailConsumer>();

builder.Services.AddHostedService<StateVectorEmitterService>();

builder.Services.AddHealthChecks()
    .AddCheck<FeatureEngineeringHealthCheck>("feature-engineering")
    .AddNpgSql(postgresConnectionString, name: "postgres");

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = FeatureEngineeringHealthCheck.WriteResponse
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapStateEndpoints();

await app.Services.GetRequiredService<IStateVectorPublisher>()
    .EnsureReadyAsync(CancellationToken.None);

app.Run();

public partial class Program;