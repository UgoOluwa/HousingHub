namespace HousingHub.Service.Commons.Geocoding;

public interface IGeocodingService
{
    /// <summary>Resolves a free-text address into coordinates, or null if it couldn't be geocoded.</summary>
    Task<(double Latitude, double Longitude)?> GeocodeAsync(string? place, string? city, string? state, string? country);
}
