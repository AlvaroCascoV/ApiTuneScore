using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Services;

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly TuneScoreContext _context;

    private const string BaseUrl = "https://nominatim.openstreetmap.org/search";
    private const string UserAgent = "TuneScoreApp/1.0 (alvarocasco0807@gmail.com)";

    public GeocodingService(HttpClient httpClient, TuneScoreContext context)
    {
        _httpClient = httpClient;
        _context = context;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string city, string country)
    {
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(country)) return null;

        string normalizedCity = city.Trim().ToLower();
        string normalizedCountry = country.Trim().ToLower();

        var cached = await _context.CityLocations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.City.ToLower() == normalizedCity && c.Country.ToLower() == normalizedCountry);
        if (cached != null) return (cached.Latitude, cached.Longitude);

        try
        {
            var query = Uri.EscapeDataString(city + ", " + country);
            var uri = $"{BaseUrl}?q={query}&format=json&limit=1";
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(uri, cts.Token);
            var first = results?.FirstOrDefault();
            if (first == null) return null;

            if (!double.TryParse(first.lat, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(first.lon, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                return null;

            _context.CityLocations.Add(new CityLocation { City = normalizedCity, Country = normalizedCountry, Latitude = lat, Longitude = lon, LastUpdated = DateTime.Now });
            await _context.SaveChangesAsync();
            return (lat, lon);
        }
        catch { return null; }
    }

    private class NominatimResult
    {
        public string lat { get; set; } = string.Empty;
        public string lon { get; set; } = string.Empty;
    }
}
