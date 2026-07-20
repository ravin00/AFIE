using System.Net;
using System.Text;
using AFIE.Telemetry.Clients;
using Microsoft.Extensions.Logging;
using Moq;

namespace AFIE.Telemetry.Tests.Clients;

public class PrometheusHttpClientTests
{
    private static PrometheusHttpClient CreateClient(HttpResponseMessage response)
    {
        var handler = new FakeHttpHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9090") };
        var logger = Mock.Of<ILogger<PrometheusHttpClient>>();
        return new PrometheusHttpClient(httpClient, logger);
    }

    [Fact]
    public async Task QueryAsync_SuccessResponse_ReturnsResults()
    {
        var json = await File.ReadAllTextAsync("Fixtures/prometheus_instant_response.json");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var client = CreateClient(response);
        var results = await client.QueryAsync("up");

        Assert.Equal(2, results.Count);
        Assert.Equal("nginx-deployment-5d6f7b8c9-x4k2j", results[0].Metric["pod"]);
    }

    [Fact]
    public async Task QueryAsync_ErrorResponse_ReturnsEmptyList()
    {
        var json = await File.ReadAllTextAsync("Fixtures/prometheus_error_response.json");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var client = CreateClient(response);
        var results = await client.QueryAsync("invalid{}");

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_HttpError_ReturnsEmptyList()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = CreateClient(response);
        var results = await client.QueryAsync("up");

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_MalformedJson_ReturnsEmptyList()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not json at all", Encoding.UTF8, "application/json")
        };

        var client = CreateClient(response);
        var results = await client.QueryAsync("up");

        Assert.Empty(results);
    }
}

public class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public FakeHttpHandler(HttpResponseMessage response) => _response = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(_response);
}
