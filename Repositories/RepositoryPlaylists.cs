using Microsoft.EntityFrameworkCore;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class RepositoryPlaylists : IRepositoryPlaylists
{
    private readonly TuneScoreContext _context;

    public RepositoryPlaylists(TuneScoreContext context) => _context = context;

    public async Task<IEnumerable<Playlist>> GetPlaylistsByUserIdAsync(int userId)
        => await _context.Playlists.Include(p => p.PlaylistSongs).Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<IEnumerable<Playlist>> GetPlaylistsFromOtherUsersAsync(int excludeUserId, int count = 12)
        => await _context.Playlists.Include(p => p.User).Include(p => p.PlaylistSongs).Where(p => p.UserId != excludeUserId).OrderByDescending(p => p.CreatedAt).Take(count).ToListAsync();

    public async Task<Playlist?> GetPlaylistByIdAsync(int id)
        => await _context.Playlists
            .Include(p => p.User)
            .Include(p => p.PlaylistSongs).ThenInclude(ps => ps.Song).ThenInclude(s => s!.Album).ThenInclude(a => a!.Artist)
            .Include(p => p.PlaylistSongs).ThenInclude(ps => ps.Song).ThenInclude(s => s!.Genre)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<int> AddPlaylistAsync(int userId, string name, string? description, string? imageName, DateTime createdAt)
    {
        var playlist = new Playlist { UserId = userId, Name = name, Description = description, ImageName = imageName, CreatedAt = createdAt };
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist.Id;
    }

    public async Task<bool> UpdatePlaylistAsync(int id, string name, string? description, string? imageName)
    {
        var playlist = await _context.Playlists.FindAsync(id);
        if (playlist == null) return false;
        playlist.Name = name; playlist.Description = description; playlist.ImageName = imageName;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task DeletePlaylistAsync(int id)
    {
        var playlist = await _context.Playlists.FindAsync(id);
        if (playlist != null) { _context.Playlists.Remove(playlist); await _context.SaveChangesAsync(); }
    }

    public async Task<bool> AddSongToPlaylistAsync(int playlistId, int songId)
    {
        var exists = await _context.PlaylistSongs.AnyAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);
        if (exists) return false;

        var positions = await _context.PlaylistSongs.Where(ps => ps.PlaylistId == playlistId).Select(ps => ps.Position).ToListAsync();
        var maxPosition = positions.Count > 0 ? positions.Max() : 0;

        _context.PlaylistSongs.Add(new PlaylistSong { PlaylistId = playlistId, SongId = songId, Position = maxPosition + 1 });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> AddAlbumToPlaylistAsync(int playlistId, int albumId)
    {
        var albumSongIds = await _context.Songs.Where(s => s.AlbumId == albumId).Select(s => s.Id).ToListAsync();
        var existingInPlaylist = await _context.PlaylistSongs.Where(ps => ps.PlaylistId == playlistId).Select(ps => ps.SongId).ToListAsync();
        var toAdd = albumSongIds.Except(existingInPlaylist).ToList();
        var positions = await _context.PlaylistSongs.Where(ps => ps.PlaylistId == playlistId).Select(ps => ps.Position).ToListAsync();
        var pos = positions.Count > 0 ? positions.Max() : 0;
        foreach (var songId in toAdd)
        {
            pos++;
            _context.PlaylistSongs.Add(new PlaylistSong { PlaylistId = playlistId, SongId = songId, Position = pos });
        }
        await _context.SaveChangesAsync();
        return toAdd.Count;
    }

    public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId)
    {
        var ps = await _context.PlaylistSongs.FirstOrDefaultAsync(x => x.PlaylistId == playlistId && x.SongId == songId);
        if (ps != null)
        {
            _context.PlaylistSongs.Remove(ps);
            await _context.SaveChangesAsync();

            var remaining = await _context.PlaylistSongs.Where(x => x.PlaylistId == playlistId).OrderBy(x => x.Position).ToListAsync();
            for (var i = 0; i < remaining.Count; i++) remaining[i].Position = i + 1;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ReorderPlaylistAsync(int playlistId, IReadOnlyList<int> songIdsInOrder)
    {
        var playlistSongs = await _context.PlaylistSongs.Where(ps => ps.PlaylistId == playlistId).ToListAsync();
        var bySongId = playlistSongs.ToDictionary(ps => ps.SongId);
        for (var i = 0; i < songIdsInOrder.Count; i++)
        {
            if (bySongId.TryGetValue(songIdsInOrder[i], out var psi)) psi.Position = i + 1;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Song>> GetRecommendedSongsAsync(int count, int? excludePlaylistId = null, int? excludeSongId = null, int? fromAlbumId = null)
    {
        var query = _context.Songs.Include(s => s.Album).Include(s => s.Genre).AsQueryable();
        if (excludeSongId.HasValue) query = query.Where(s => s.Id != excludeSongId.Value);
        if (excludePlaylistId.HasValue)
        {
            var inPlaylist = await _context.PlaylistSongs.Where(ps => ps.PlaylistId == excludePlaylistId.Value).Select(ps => ps.SongId).ToListAsync();
            query = query.Where(s => !inPlaylist.Contains(s.Id));
        }
        if (fromAlbumId.HasValue) query = query.Where(s => s.AlbumId == fromAlbumId.Value);
        return await query.OrderByDescending(s => s.CreatedAt).Take(count).ToListAsync();
    }
}
