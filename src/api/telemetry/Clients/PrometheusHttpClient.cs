using System.Text.Json;
using AFIE.Telemetry.Models;

namespace AFIE.Telemetry.Clients;

public class PrometheusHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PrometheusHttpClient> _logger;

    public PrometheusHttpClient(HttpClient http, ILogger<PrometheusHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<PrometheusResult>> QueryAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/query?query={Uri.EscapeDataString(query)}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<PrometheusApiResponse>(json);

            if (parsed?.Status != "success" || parsed.Data is null)
            {
                _logger.LogWarning("Prometheus query failed: {Error}", parsed?.Error);
                return [];
            }

            return parsed.Data.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prometheus query failed for: {Query}", query);
            return [];
        }
    }

    public async Task<List<PrometheusResult>> QueryRangeAsync(
        string query, DateTimeOffset start, DateTimeOffset end, string step, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}" +
                      $"&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={step}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<PrometheusApiResponse>(json);

            if (parsed?.Status != "success" || parsed.Data is null)
            {
                _logger.LogWarning("Prometheus range query failed: {Error}", parsed?.Error);
                return [];
            }

            return parsed.Data.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prometheus range query failed for: {Query}", query);
            return [];
        }
    }
}