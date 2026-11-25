using System.Net;
using System.Text;
using AFIE.Contracts;
using AFIE.Telemetry.Clients;
using AFIE.Telemetry.Health;
using AFIE.Telemetry.Models;
using AFIE.Telemetry.Publishers;
using AFIE.Telemetry.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AFIE.Telemetry.Tests.Services;

public class PrometheusScraperServiceTests
{
    private static readonly IOptions<TelemetryOptions> Config = Options.Create(new TelemetryOptions());

    private static PrometheusScraperService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond, IMetricPublisher publisher, TelemetryHealthState state)
    {
        var http = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("http://localhost:9090") };
        var client = new PrometheusHttpClient(http, NullLogger<PrometheusHttpClient>.Instance);
        return new PrometheusScraperService(
            client, publisher, state, Config, NullLogger<PrometheusScraperService>.Instance);
    }

    public static TheoryData<Func<HttpRequestMessage, HttpResponseMessage>> Outages => new()
    {
        _ => throw new HttpRequestException("connection refused"),
        _ => throw new TaskCanceledException("HttpClient timeout"),
        _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
    };

    [Theory]
    [MemberData(nameof(Outages))]
    public async Task Health_PrometheusOutageAfterGoodScrape_IsDegraded(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var state = new TelemetryHealthState { LastScrapeTime = DateTimeOffset.UtcNow, PrometheusReachable = true };
        var publisher = new Mock<IMetricPublisher>();
        using var service = CreateService(respond, publisher.Object, state);

        await service.StartAsync(CancellationToken.None);
        SpinWait.SpinUntil(() => !state.PrometheusReachable, TimeSpan.FromSeconds(5));

        var health = await new TelemetryHealthCheck(state, Config).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, health.Status);
        Assert.False(service.ExecuteTask!.IsCompleted); // the loop survives, even on a timeout
        publisher.VerifyNoOtherCalls();

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Scrape_MultiContainerPod_SumsRequestsAndLimits()
    {
        HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            var values = query switch
            {
                _ when query.Contains("requests{resource=\"cpu\"}") => new[] { "0.1", "0.25" },
                _ when query.Contains("limits{resource=\"cpu\"}") => ["0.5", "1"],
                _ when query.Contains("requests{resource=\"memory\"}") => ["100", "200"],
                _ when query.Contains("limits{resource=\"memory\"}") => ["300", "400"],
                _ => [],
            };
            var series = string.Join(",", values.Select((v, i) =>
                $$$"""{"metric":{"pod":"api-0","namespace":"ns","container":"c{{{i}}}"},"value":[1,"{{{v}}}"]}"""));
            var json = $$$"""{"status":"success","data":{"resultType":"vector","result":[{{{series}}}]}}""";
            return new HttpResponseMessage { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        var published = new TaskCompletionSource<IReadOnlyList<MetricEvent>>();
        var publisher = new Mock<IMetricPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<MetricEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<MetricEvent> events, CancellationToken _) => published.TrySetResult(events))
            .Returns(Task.CompletedTask);
        using var service = CreateService(Respond, publisher.Object, new TelemetryHealthState());

        await service.StartAsync(CancellationToken.None);
        var metric = Assert.Single(await published.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0.35, metric.CpuRequest, 6);
        Assert.Equal(1.5, metric.CpuLimit, 6);
        Assert.Equal(300, metric.MemRequest);
        Assert.Equal(700, metric.MemLimit);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }
}
