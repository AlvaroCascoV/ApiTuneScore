using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IRepositoryPlaylists
{
    Task<IEnumerable<Playlist>> GetPlaylistsByUserIdAsync(int userId);
    Task<IEnumerable<Playlist>> GetPlaylistsFromOtherUsersAsync(int excludeUserId, int count = 12);
    Task<Playlist?> GetPlaylistByIdAsync(int id);
    Task<int> AddPlaylistAsync(int userId, string name, string? description, string? imageName, DateTime createdAt);
    Task<bool> UpdatePlaylistAsync(int id, string name, string? description, string? imageName);
    Task DeletePlaylistAsync(int id);
    Task<bool> AddSongToPlaylistAsync(int playlistId, int songId);
    Task<int> AddAlbumToPlaylistAsync(int playlistId, int albumId);
    Task RemoveSongFromPlaylistAsync(int playlistId, int songId);
    Task ReorderPlaylistAsync(int playlistId, IReadOnlyList<int> songIdsInOrder);
    Task<IEnumerable<Song>> GetRecommendedSongsAsync(int count, int? excludePlaylistId = null, int? excludeSongId = null, int? fromAlbumId = null);
}
