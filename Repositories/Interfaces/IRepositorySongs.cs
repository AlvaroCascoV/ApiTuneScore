using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IRepositorySongs
{
    Task<(IEnumerable<SongListItemDto> Items, int TotalCount)> GetSongsPageAsync(int pageNumber, int pageSize);
    Task<(IEnumerable<SongListItemDto> Items, int TotalCount)> GetSongsPageAsync(int pageNumber, int pageSize, string? q);
    Task<IEnumerable<Song>> GetAllSongsAsync();
    Task<IEnumerable<Song>> GetSongsByAlbumAsync(int albumId);
    Task<Song?> GetSongByIdAsync(int id);
    Task<int> AddSongAsync(string title, int? durationSeconds, int albumId, int genreId, DateTime createdAt);
    Task<int> AddSongAsync(string title, int? durationSeconds, int albumId, int genreId, DateTime createdAt, string contentStatus);
    Task<bool> UpdateSongAsync(int id, string title, int? durationSeconds, int albumId, int genreId);
    Task DeleteSongAsync(int id);
    Task<List<Song>> GetSongsByArtistIdAsync(int artistId);
    Task SetDeleteRequestedAsync(int id, bool requested);
    Task<List<Song>> GetPendingSongsAsync();
    Task<List<Song>> GetDeleteRequestedSongsAsync();
    Task ApproveSongAsync(int id);
    Task<bool> RejectPendingSongAsync(int id);
}
