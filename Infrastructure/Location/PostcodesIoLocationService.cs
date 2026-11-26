using Core.Interfaces;
using System.Text.Json;

namespace Infrastructure.Location;

/// <summary>
/// Uses api.postcodes.io — free UK postcode lookup API.
/// Returns lat/lng and automatically resolves London borough.
/// For other countries, a different provider would be injected.
/// </summary>
public class PostcodesIoLocationService : ILocationService
{
    private readonly HttpClient _http;

    public PostcodesIoLocationService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("PostcodesIO");
        _http.BaseAddress = new Uri("https://api.postcodes.io/");
    }

    public async Task<(decimal Lat, decimal Lng, string? Region, string? City)?> LookupAsync(
        string postalCode,
        string countryCode)
    {
        // Only supports UK postcodes for now
        if (countryCode != "GB") return null;

        var cleaned  = postalCode.Replace(" ", "").ToUpperInvariant();
        var response = await _http.GetAsync($"postcodes/{cleaned}");

        if (!response.IsSuccessStatusCode) return null;

        var json     = await response.Content.ReadAsStringAsync();
        var doc      = JsonDocument.Parse(json);
        var result   = doc.RootElement.GetProperty("result");

        var lat     = result.GetProperty("latitude").GetDecimal();
        var lng     = result.GetProperty("longitude").GetDecimal();
        var borough = result.TryGetProperty("admin_district", out var d) ? d.GetString() : null;
        var city    = "London"; // All UK postcodes we handle are London

        return (lat, lng, borough, city);
    }
}
