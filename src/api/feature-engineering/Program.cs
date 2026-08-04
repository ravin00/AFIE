using AFIE.FeatureEngineering.Consumers;
using AFIE.FeatureEngineering.Endpoints;
using AFIE.FeatureEngineering.Features;
using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var section = builder.Configuration.GetSection("FeatureEngineering");
builder.Services.Configure<FeatureEngineeringOptions>(section);
builder.Services.Configure<EventHubOptions>(builder.Configuration.GetSection("EventHub"));

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

var consumerMode = section["ConsumerMode"] ?? "local";
if (consumerMode == "eventhub")
    builder.Services.AddHostedService<EventHubConsumer>();
else
    builder.Services.AddHostedService<LocalJsonlTailConsumer>();

builder.Services.AddHealthChecks()
    .AddCheck<FeatureEngineeringHealthCheck>("feature-engineering");

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = FeatureEngineeringHealthCheck.WriteResponse
});
app.MapStateEndpoints();

app.Run();

public partial class Program;