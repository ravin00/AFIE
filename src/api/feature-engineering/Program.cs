using AFIE.FeatureEngineering.Consumers;
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

app.Run();