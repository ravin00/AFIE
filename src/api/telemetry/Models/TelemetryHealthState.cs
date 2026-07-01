namespace AFIE.Telemetry.Models;

public class TelemetryHealthState
{
    public DateTimeOffset? LastScrapeTime {get;set;}
    public long EventPublishTotal {get;set;}
    public bool PrometheusReachable {get;set;}
}