namespace ApiTuneScore.Services.Interfaces;

public interface IGeocodingService
{
    Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string city, string country);
}
