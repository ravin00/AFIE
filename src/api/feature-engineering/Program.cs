using AFIE.FeatureEngineering.Health;
using AFIE.FeatureEngineering.Models;
using AFIE.FeatureEngineering.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FeatureEngineeringOptions>(
    builder.Configuration.GetSection("FeatureEngineering"));

builder.Services.AddSingleton<FeatureEngineeringHealthState>();
builder.Services.AddSingleton<ActionHistoryStore>();
builder.Services.AddSingleton<WindowStore>();

builder.Services.AddHealthChecks()
    .AddCheck<FeatureEngineeringHealthCheck>("feature-engineering");

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = FeatureEngineeringHealthCheck.WriteResponse
});

app.Run();