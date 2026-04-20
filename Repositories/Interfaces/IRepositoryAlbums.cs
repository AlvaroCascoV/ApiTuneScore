using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IRepositoryAlbums
{
    Task<(IEnumerable<AlbumListItemDto> Items, int TotalCount)> GetAlbumsPageAsync(int pageNumber, int pageSize);
    Task<(IEnumerable<AlbumListItemDto> Items, int TotalCount)> GetAlbumsPageAsync(int pageNumber, int pageSize, string? q);
    Task<IEnumerable<Album>> GetAllAlbumsAsync();
    Task<Album?> GetAlbumByIdAsync(int id);
    Task<int> AddAlbumAsync(string title, int releaseYear, int artistId, string? imageName, DateTime createdAt);
    Task<int> AddAlbumAsync(string title, int releaseYear, int artistId, string? imageName, DateTime createdAt, string contentStatus);
    Task<bool> UpdateAlbumAsync(int id, string title, int releaseYear, int artistId, string? imageName);
    Task DeleteAlbumAsync(int id);
    Task<List<Album>> GetAlbumsByArtistIdAsync(int artistId);
    Task SetDeleteRequestedAsync(int id, bool requested);
    Task<List<Album>> GetPendingAlbumsAsync();
    Task<List<Album>> GetDeleteRequestedAlbumsAsync();
    Task ApproveAlbumAsync(int id);
    Task<bool> RejectPendingAlbumAsync(int id);
}
