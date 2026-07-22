using AFIE.Telemetry.Models;
using AFIE.Telemetry.Publishers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AFIE.Telemetry.Tests.Publishers;

public class MetricPublisherStrategyResolverTests
{
    [Fact]
    public void Resolve_WithLocalMode_ReturnsLocalFilePublisher()
    {
        var local = BuildLocalPublisher();
        var resolver = BuildResolver(local, eventHubFactory: FailIfBuilt, mode: "local");

        var result = resolver.Resolve();

        Assert.Same(local, result);
    }

    [Fact]
    public void Resolve_WithEventHubMode_ReturnsEventHubPublisher()
    {
        var local = BuildLocalPublisher();
        var eventHub = BuildEventHubPublisher();
        var resolver = BuildResolver(local, eventHubFactory: () => eventHub, mode: "eventhub");

        var result = resolver.Resolve();

        Assert.Same(eventHub, result);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var local = BuildLocalPublisher();
        var resolver = BuildResolver(local, eventHubFactory: FailIfBuilt, mode: "LOCAL");

        var result = resolver.Resolve();

        Assert.Same(local, result);
    }

    [Fact]
    public void Resolve_WithUnknownMode_ThrowsInvalidOperationException()
    {
        var resolver = BuildResolver(
            BuildLocalPublisher(),
            eventHubFactory: FailIfBuilt,
            mode: "bogus");

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve());
    }

    [Fact]
    public void Resolve_WithUnknownMode_ExceptionListsAvailableModes()
    {
        var resolver = BuildResolver(
            BuildLocalPublisher(),
            eventHubFactory: FailIfBuilt,
            mode: "bogus");

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve());

        Assert.Contains("bogus", ex.Message);
        Assert.Contains(LocalFilePublisher.ModeName, ex.Message);
        Assert.Contains(EventHubPublisher.ModeName, ex.Message);
    }

    private static MetricPublisherStrategyResolver BuildResolver(
        LocalFilePublisher local,
        Func<EventHubPublisher> eventHubFactory,
        string mode)
    {
        var options = Options.Create(new TelemetryOptions { OutputMode = mode });
        return new MetricPublisherStrategyResolver(
            new Lazy<LocalFilePublisher>(() => local),
            new Lazy<EventHubPublisher>(eventHubFactory),
            options);
    }

    private static LocalFilePublisher BuildLocalPublisher()
    {
        var options = Options.Create(new TelemetryOptions
        {
            OutputPath = Path.Combine(Path.GetTempPath(), $"afie_resolver_{Guid.NewGuid():N}")
        });
        return new LocalFilePublisher(options, Mock.Of<ILogger<LocalFilePublisher>>());
    }

    private static EventHubPublisher BuildEventHubPublisher()
    {
        var options = Options.Create(new EventHubOptions
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            EventHubName = "test-hub"
        });
        return new EventHubPublisher(options, Mock.Of<ILogger<EventHubPublisher>>());
    }

    private static EventHubPublisher FailIfBuilt() =>
        throw new Xunit.Sdk.XunitException(
            "EventHubPublisher should not have been instantiated for this test.");
}
