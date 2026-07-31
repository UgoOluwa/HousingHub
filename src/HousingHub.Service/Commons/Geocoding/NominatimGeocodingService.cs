using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.Geocoding;

// Nominatim (OpenStreetMap) usage policy requires a descriptive User-Agent and caps
// requests at ~1/second — fine for this app's per-property create/update volume.
// https://operations.osmfoundation.org/policies/nominatim/
internal sealed class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(HttpClient httpClient, ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HousingHub/1.0 (property-geocoding)");
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(string? place, string? city, string? state, string? country)
    {
        var parts = new[] { place, city, state, string.IsNullOrWhiteSpace(country) ? "Nigeria" : country }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var query = string.Join(", ", parts);

        if (string.IsNullOrWhiteSpace(query))
            return null;

        try
        {
            var url = $"search?q={HttpUtility.UrlEncode(query)}&format=json&limit=1";
            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url);
            var match = results?.FirstOrDefault();

            if (match == null
                || !double.TryParse(match.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(match.Lon, System.Globalization.CultureInfo.InvariantCulture, out var lon))
                return null;

            return (lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to geocode address \"{Query}\"", query);
            return null;
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
