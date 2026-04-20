using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class RepositorySongs : IRepositorySongs
{
    private readonly TuneScoreContext _context;
    private const string GetSongsPageSql = "EXEC SP_PAGINACION_CANCIONES @PageNumber, @PageSize, @TotalCount OUTPUT";

    public RepositorySongs(TuneScoreContext context) => _context = context;

    public async Task<(IEnumerable<SongListItemDto> Items, int TotalCount)> GetSongsPageAsync(int pageNumber, int pageSize)
    {
        var pageParam = new SqlParameter("@PageNumber", pageNumber);
        var sizeParam = new SqlParameter("@PageSize", pageSize);
        var totalParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

        var items = await _context.SongListItems
            .FromSqlRaw(GetSongsPageSql, pageParam, sizeParam, totalParam)
            .ToListAsync();

        var totalCount = totalParam.Value != null && totalParam.Value != DBNull.Value
            ? Convert.ToInt32(totalParam.Value) : 0;

        return (items, totalCount);
    }

    public async Task<(IEnumerable<SongListItemDto> Items, int TotalCount)> GetSongsPageAsync(int pageNumber, int pageSize, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return await GetSongsPageAsync(pageNumber, pageSize);

        var term = q.Trim();
        IQueryable<Song> query = _context.Songs.AsNoTracking()
            .Include(s => s.Album).ThenInclude(a => a.Artist)
            .Include(s => s.Genre)
            .Where(s => s.ContentStatus == ContentStatuses.Approved
                && s.Album.ContentStatus == ContentStatuses.Approved
                && s.Album.Artist.Status == ArtistStatuses.Active
                && (s.Title.Contains(term)
                    || (s.Album != null && s.Album.Title.Contains(term))
                    || (s.Album != null && s.Album.Artist != null && s.Album.Artist.Name.Contains(term))
                    || (s.Genre != null && s.Genre.Name.Contains(term))));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SongListItemDto
            {
                Id = s.Id, Title = s.Title, DurationSeconds = s.DurationSeconds,
                AlbumTitle = s.Album != null ? s.Album.Title : null,
                AlbumImageName = s.Album != null ? s.Album.ImageName : null,
                GenreName = s.Genre != null ? s.Genre.Name : null
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Song>> GetAllSongsAsync()
        => await _context.Songs.Include(s => s.Album).Include(s => s.Genre).ToListAsync();

    public async Task<IEnumerable<Song>> GetSongsByAlbumAsync(int albumId)
        => await _context.Songs.Include(s => s.Album).Include(s => s.Genre).Where(s => s.AlbumId == albumId).ToListAsync();

    public async Task<Song?> GetSongByIdAsync(int id)
        => await _context.Songs.Include(s => s.Album).ThenInclude(a => a.Artist).Include(s => s.Genre).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<int> AddSongAsync(string title, int? durationSeconds, int albumId, int genreId, DateTime createdAt)
        => await AddSongAsync(title, durationSeconds, albumId, genreId, createdAt, ContentStatuses.Approved);

    public async Task<int> AddSongAsync(string title, int? durationSeconds, int albumId, int genreId, DateTime createdAt, string contentStatus)
    {
        var song = new Song { Title = title, DurationSeconds = durationSeconds, AlbumId = albumId, GenreId = genreId, CreatedAt = createdAt, ContentStatus = contentStatus };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();
        return song.Id;
    }

    public async Task<bool> UpdateSongAsync(int id, string title, int? durationSeconds, int albumId, int genreId)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null) return false;
        song.Title = title; song.DurationSeconds = durationSeconds; song.AlbumId = albumId; song.GenreId = genreId;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task DeleteSongAsync(int id)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song != null) { _context.Songs.Remove(song); await _context.SaveChangesAsync(); }
    }

    public async Task<List<Song>> GetSongsByArtistIdAsync(int artistId)
        => await _context.Songs.Include(s => s.Album).Include(s => s.Genre).Where(s => s.Album.ArtistId == artistId).OrderByDescending(s => s.CreatedAt).ToListAsync();

    public async Task SetDeleteRequestedAsync(int id, bool requested)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null) return;
        song.DeleteRequested = requested;
        song.DeleteRequestedAt = requested ? DateTime.Now : null;
        await _context.SaveChangesAsync();
    }

    public async Task<List<Song>> GetPendingSongsAsync()
        => await _context.Songs.Include(s => s.Album).ThenInclude(a => a.Artist).Include(s => s.Genre).Where(s => s.ContentStatus == ContentStatuses.Pending).OrderBy(s => s.CreatedAt).ToListAsync();

    public async Task<List<Song>> GetDeleteRequestedSongsAsync()
        => await _context.Songs.Include(s => s.Album).ThenInclude(a => a.Artist).Where(s => s.DeleteRequested).OrderBy(s => s.DeleteRequestedAt).ToListAsync();

    public async Task ApproveSongAsync(int id)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null) return;
        song.ContentStatus = ContentStatuses.Approved;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RejectPendingSongAsync(int id)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null || song.ContentStatus != ContentStatuses.Pending) return false;
        _context.Songs.Remove(song);
        await _context.SaveChangesAsync();
        return true;
    }
}
