using System.Net;
using System.Net.Http.Json;
using AFIE.Contracts;
using AFIE.FeatureEngineering.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFIE.FeatureEngineering.Tests.Endpoints;

public class StateEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StateEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Get_UnknownWorkload_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/state/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_KnownWorkload_Returns47Values()
    {
        var store = _factory.Services.GetRequiredService<WindowStore>();
        store.Add(new MetricEvent(
            "nginx-endpoint-test", "afie-system", DateTimeOffset.UtcNow,
            0.5, 104_857_600, 10, 0, 5, 20, 50,
            false, false, 0.1, 1.0, 67_108_864, 209_715_200));

        var client = _factory.CreateClient();
        var values = await client.GetFromJsonAsync<float[]>("/state/nginx-endpoint-test");

        Assert.NotNull(values);
        Assert.Equal(47, values!.Length);
        Assert.All(values, v => Assert.True(!float.IsNaN(v) && !float.IsInfinity(v)));
    }
}
