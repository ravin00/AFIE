using System.Net;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Endpoints;

public class MetricsEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public MetricsEndpointTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_Metrics_ReturnsPrometheusExposition()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("# HELP", body);
    }
}
