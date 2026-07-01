namespace AFIE.Telemetry.Models;

public class TelemetryOptions
{
    public string PrometheusUrl { get; set; } = "http://localhost:9090";
    public int ScrapingIntervalSeconds { get; set; } = 15;
    public string OutputMode { get; set; } = "local";
    public string OutputPath { get; set; } = "experiments/results";
}

public class EventHubOptions
{
    public string FullyQualifiedNamespace {get;set;} = "";
    public string EventHubName {get;set;} = "telemetry-events";
}