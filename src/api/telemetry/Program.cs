using AFIE.Telemetry.Clients;
using AFIE.Telemetry.Health;
using AFIE.Telemetry.Models;
using AFIE.Telemetry.Publishers;
using AFIE.Telemetry.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var telemetrySection = builder.Configuration.GetSection("Telemetry");
builder.Services.Configure<TelemetryOptions>(telemetrySection);
builder.Services.Configure<EventHubOptions>(builder.Configuration.GetSection("EventHub"));

builder.Services.AddHttpClient<PrometheusHttpClient>(client =>
{
    var prometheusUrl = telemetrySection["PrometheusUrl"];
    if (string.IsNullOrWhiteSpace(prometheusUrl))
        throw new InvalidOperationException("Telemetry:PrometheusUrl must be configured.");

    client.BaseAddress = new Uri(prometheusUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<TelemetryHealthState>();

var outputMode = telemetrySection["OutputMode"] ?? "local";
if (outputMode == "eventhub")
    builder.Services.AddSingleton<IMetricPublisher, EventHubPublisher>();
else
    builder.Services.AddSingleton<IMetricPublisher, LocalFilePublisher>();

builder.Services.AddHostedService<PrometheusScraperService>();

builder.Services.AddHealthChecks()
    .AddCheck<TelemetryHealthCheck>("telemetry");

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = TelemetryHealthCheck.WriteResponse
});

app.Run();
