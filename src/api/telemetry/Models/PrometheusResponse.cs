using System.Text.Json.Serialization;

namespace AFIE.Telemetry.Models;

public record PrometheusApiResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] PrometheusData? Data,
    [property: JsonPropertyName("errorType")] string? ErrorType,
    [property: JsonPropertyName("error")] string? Error
);

public record PrometheusData(
    [property: JsonPropertyName("resultType")] string ResultType,
    [property: JsonPropertyName("result")] List<PrometheusResult> Result
);

public record PrometheusResult(
    [property: JsonPropertyName("metric")] Dictionary<string, string> Metric,
    [property: JsonPropertyName("value")] List<object>? Value,
    [property: JsonPropertyName("values")] List<List<object>>? Values
);