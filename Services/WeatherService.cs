using System.Text.Json;

namespace FlightPlanAgent.Services;

public record WeatherResult(string Icao, string? Metar, string? Taf, string? Error);

public class WeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WeatherResult> GetWeatherAsync(string icao)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        string? metar = null;
        string? taf = null;
        string? error = null;

        try
        {
            metar = await FetchRawTextAsync(client,
                $"https://aviationweather.gov/api/data/metar?ids={icao}&format=raw");
        }
        catch (Exception ex)
        {
            error = $"METAR lookup failed: {ex.Message}";
        }

        try
        {
            taf = await FetchRawTextAsync(client,
                $"https://aviationweather.gov/api/data/taf?ids={icao}&format=raw");
        }
        catch (Exception ex)
        {
            error = (error is null ? "" : error + " ") + $"TAF lookup failed: {ex.Message}";
        }

        if (string.IsNullOrWhiteSpace(metar) && string.IsNullOrWhiteSpace(taf) && error is null)
        {
            error = "No live weather data returned for this ICAO code.";
        }

        return new WeatherResult(icao.ToUpperInvariant(), metar, taf, error);
    }

    private static async Task<string> FetchRawTextAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        return text.Trim();
    }
}
