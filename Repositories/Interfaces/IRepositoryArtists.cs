using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IRepositoryArtists
{
    Task<(IEnumerable<ArtistListItemDto> Items, int TotalCount)> GetArtistsPageAsync(int pageNumber, int pageSize);
    Task<(IEnumerable<ArtistListItemDto> Items, int TotalCount)> GetArtistsPageAsync(int pageNumber, int pageSize, string? q);
    Task<List<Artist>> GetAllArtistsAsync();
    Task<List<Artist>> GetActiveArtistsAsync();
    Task<Artist?> GetArtistByIdAsync(int id);
    Task InsertArtistAsync(int id, string name, string imageName, DateTime createdAt, string? city, string? country, double? latitude, double? longitude);
    Task UpdateArtistAsync(int id, string name, string imageName, string? city, string? country, double? latitude, double? longitude);
    Task<bool> DeleteArtistAsync(int id);
    Task<int> CreateArtistAsync(string name, string? imageName, DateTime createdAt, string? city, string? country, double? latitude, double? longitude, string status, int? createdByUserId);
    Task ApproveArtistAsync(int id);
    Task SetArtistCoordsAsync(int id, double latitude, double longitude);
}
