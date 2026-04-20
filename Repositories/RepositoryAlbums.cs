using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class RepositoryAlbums : IRepositoryAlbums
{
    private readonly TuneScoreContext _context;
    private const string GetAlbumsPageSql = "EXEC SP_PAGINACION_ALBUMES @PageNumber, @PageSize, @TotalCount OUTPUT";

    public RepositoryAlbums(TuneScoreContext context) => _context = context;

    public async Task<(IEnumerable<AlbumListItemDto> Items, int TotalCount)> GetAlbumsPageAsync(int pageNumber, int pageSize)
    {
        var pageParam = new SqlParameter("@PageNumber", pageNumber);
        var sizeParam = new SqlParameter("@PageSize", pageSize);
        var totalParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

        var items = await _context.AlbumListItems
            .FromSqlRaw(GetAlbumsPageSql, pageParam, sizeParam, totalParam)
            .ToListAsync();

        var totalCount = totalParam.Value != null && totalParam.Value != DBNull.Value
            ? Convert.ToInt32(totalParam.Value) : 0;

        return (items, totalCount);
    }

    public async Task<(IEnumerable<AlbumListItemDto> Items, int TotalCount)> GetAlbumsPageAsync(int pageNumber, int pageSize, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return await GetAlbumsPageAsync(pageNumber, pageSize);

        var term = q.Trim();
        IQueryable<Album> query = _context.Albums
            .AsNoTracking()
            .Include(a => a.Artist)
            .Where(a => a.ContentStatus == ContentStatuses.Approved
                     && a.Artist.Status == ArtistStatuses.Active
                     && (a.Title.Contains(term) || a.Artist.Name.Contains(term)));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AlbumListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                ReleaseYear = a.ReleaseYear,
                ImageName = a.ImageName,
                ArtistId = a.ArtistId,
                ArtistName = a.Artist != null ? a.Artist.Name : null
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Album>> GetAllAlbumsAsync()
        => await _context.Albums.Include(a => a.Artist).ToListAsync();

    public async Task<Album?> GetAlbumByIdAsync(int id)
        => await _context.Albums
            .Include(a => a.Artist)
            .Include(a => a.Songs).ThenInclude(s => s.Genre)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<int> AddAlbumAsync(string title, int releaseYear, int artistId, string? imageName, DateTime createdAt)
        => await AddAlbumAsync(title, releaseYear, artistId, imageName, createdAt, ContentStatuses.Approved);

    public async Task<int> AddAlbumAsync(string title, int releaseYear, int artistId, string? imageName, DateTime createdAt, string contentStatus)
    {
        var album = new Album { Title = title, ReleaseYear = releaseYear, ArtistId = artistId, ImageName = imageName, CreatedAt = createdAt, ContentStatus = contentStatus };
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();
        return album.Id;
    }

    public async Task<bool> UpdateAlbumAsync(int id, string title, int releaseYear, int artistId, string? imageName)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null) return false;
        album.Title = title; album.ReleaseYear = releaseYear; album.ArtistId = artistId; album.ImageName = imageName;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album != null) { _context.Albums.Remove(album); await _context.SaveChangesAsync(); }
    }

    public async Task<List<Album>> GetAlbumsByArtistIdAsync(int artistId)
        => await _context.Albums.Include(a => a.Artist).Where(a => a.ArtistId == artistId).OrderByDescending(a => a.CreatedAt).ToListAsync();

    public async Task SetDeleteRequestedAsync(int id, bool requested)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null) return;
        album.DeleteRequested = requested;
        album.DeleteRequestedAt = requested ? DateTime.Now : null;
        await _context.SaveChangesAsync();
    }

    public async Task<List<Album>> GetPendingAlbumsAsync()
        => await _context.Albums.Include(a => a.Artist).Where(a => a.ContentStatus == ContentStatuses.Pending).OrderBy(a => a.CreatedAt).ToListAsync();

    public async Task<List<Album>> GetDeleteRequestedAlbumsAsync()
        => await _context.Albums.Include(a => a.Artist).Where(a => a.DeleteRequested).OrderBy(a => a.DeleteRequestedAt).ToListAsync();

    public async Task ApproveAlbumAsync(int id)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null) return;
        album.ContentStatus = ContentStatuses.Approved;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RejectPendingAlbumAsync(int id)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null || album.ContentStatus != ContentStatuses.Pending) return false;
        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();
        return true;
    }
}
