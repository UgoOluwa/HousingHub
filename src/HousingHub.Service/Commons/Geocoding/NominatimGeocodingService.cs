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

    /// <summary>
    /// Resolves an address to coordinates, narrowing the query until something
    /// matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to try the full address once and give up. Nigerian street addresses
    /// frequently do not exist in OpenStreetMap, so that failed often — and a
    /// property with no coordinates never appears under "properties near you",
    /// because GetNearbyPropertiesAsync skips anything with a null latitude. The
    /// listing was simply invisible to the feature, silently.
    /// </para>
    /// <para>
    /// So it now falls back: full address, then city and state, then state alone.
    /// "Ikeja, Lagos, Nigeria" resolves where "14b Adeniyi Jones Crescent, Ikeja,
    /// Lagos, Nigeria" does not, and a listing placed in the right city is far more
    /// useful to a nearby search than one placed nowhere.
    /// </para>
    /// <para>
    /// The precision that costs is real and the caller is told which level matched,
    /// so a coarse result can be labelled rather than presented as exact. Attempts
    /// are spaced because Nominatim's usage policy caps requests at roughly one a
    /// second and answers a burst with 429.
    /// </para>
    /// </remarks>
    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(string? place, string? city, string? state, string? country)
    {
        var resolvedCountry = string.IsNullOrWhiteSpace(country) ? "Nigeria" : country;

        // Most specific first. Each entry is only attempted if it differs from the
        // one before, so a listing with no `place` does not query the same string
        // twice and burn a request against the rate limit.
        var attempts = new List<(string Precision, string Query)>();
        void Consider(string precision, params string?[] segments)
        {
            var query = string.Join(", ", segments.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(query) && attempts.All(a => a.Query != query))
                attempts.Add((precision, query));
        }

        Consider("exact", place, city, state, resolvedCountry);
        Consider("city", city, state, resolvedCountry);
        Consider("state", state, resolvedCountry);

        if (attempts.Count == 0)
        {
            _logger.LogWarning("Geocoding skipped: no address parts supplied");
            return null;
        }

        for (var i = 0; i < attempts.Count; i++)
        {
            var (precision, query) = attempts[i];

            if (i > 0)
                await Task.Delay(TimeSpan.FromSeconds(1));

            var coordinates = await TryGeocodeAsync(query);
            if (coordinates is not null)
            {
                if (precision != "exact")
                {
                    // Worth a line: the listing will appear in nearby results, but
                    // centred on a city or a state rather than on itself.
                    _logger.LogInformation(
                        "Geocoded \"{Query}\" at {Precision} precision after the more specific address did not match",
                        query, precision);
                }

                return coordinates;
            }
        }

        _logger.LogWarning(
            "Geocoding found nothing for any of {AttemptCount} queries, the broadest being \"{Broadest}\". "
            + "This property will not appear under \"properties near you\".",
            attempts.Count, attempts[^1].Query);

        return null;
    }

    private async Task<(double Latitude, double Longitude)?> TryGeocodeAsync(string query)
    {
        try
        {
            var url = $"search?q={HttpUtility.UrlEncode(query)}&format=json&limit=1";
            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url);
            var match = results?.FirstOrDefault();

            if (match == null
                || !double.TryParse(match.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(match.Lon, System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                return null;
            }

            return (lat, lon);
        }
        catch (Exception ex)
        {
            // Includes the 429 the usage policy threatens. Logged at Warning rather
            // than Error: the caller treats a failure as "no coordinates" and carries
            // on, and a geocoding outage is not a reason to page anyone.
            _logger.LogWarning(ex, "Failed to geocode \"{Query}\"", query);
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
