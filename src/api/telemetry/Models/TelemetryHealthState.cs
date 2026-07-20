namespace AFIE.Telemetry.Models;

public class TelemetryHealthState
{
    public DateTimeOffset? LastScrapeTime { get; set; }
    public long EventsPublishedTotal { get; set; }
    public bool PrometheusReachable { get; set; }
}