using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class RepositoryArtists : IRepositoryArtists
{
    private readonly TuneScoreContext _context;
    private const string GetArtistsPageSql = "EXEC SP_PAGINACION_ARTISTAS @PageNumber, @PageSize, @TotalCount OUTPUT";

    public RepositoryArtists(TuneScoreContext context) => _context = context;

    public async Task<(IEnumerable<ArtistListItemDto> Items, int TotalCount)> GetArtistsPageAsync(int pageNumber, int pageSize)
    {
        var pageParam = new SqlParameter("@PageNumber", pageNumber);
        var sizeParam = new SqlParameter("@PageSize", pageSize);
        var totalParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

        var items = await _context.ArtistListItems
            .FromSqlRaw(GetArtistsPageSql, pageParam, sizeParam, totalParam)
            .ToListAsync();

        var totalCount = totalParam.Value != null && totalParam.Value != DBNull.Value
            ? Convert.ToInt32(totalParam.Value) : 0;

        return (items, totalCount);
    }

    public async Task<(IEnumerable<ArtistListItemDto> Items, int TotalCount)> GetArtistsPageAsync(int pageNumber, int pageSize, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return await GetArtistsPageAsync(pageNumber, pageSize);

        var term = q.Trim();
        IQueryable<Artist> query = _context.Artists.AsNoTracking()
            .Where(a => a.Status == ArtistStatuses.Active && a.Name.Contains(term));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArtistListItemDto { Id = a.Id, Name = a.Name, ImageName = a.ImageName, Country = a.Country })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Artist>> GetAllArtistsAsync()
        => await _context.Artists.OrderBy(a => a.Name).ToListAsync();

    public async Task<List<Artist>> GetActiveArtistsAsync()
        => await _context.Artists.Where(a => a.Status == ArtistStatuses.Active).OrderBy(a => a.Name).ToListAsync();

    public async Task<Artist?> GetArtistByIdAsync(int id)
        => await _context.Artists.Include(a => a.Albums).FirstOrDefaultAsync(a => a.Id == id);

    public async Task InsertArtistAsync(int id, string name, string imageName, DateTime createdAt, string? city, string? country, double? latitude, double? longitude)
    {
        var artist = new Artist { Name = name, ImageName = imageName, CreatedAt = createdAt, City = city, Country = country, Latitude = latitude, Longitude = longitude, Status = ArtistStatuses.Active };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateArtistAsync(int id, string name, string imageName, string? city, string? country, double? latitude, double? longitude)
    {
        var artist = await _context.Artists.FindAsync(id);
        if (artist == null) return;
        artist.Name = name; artist.ImageName = imageName; artist.City = city; artist.Country = country; artist.Latitude = latitude; artist.Longitude = longitude;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteArtistAsync(int id)
    {
        var artist = await _context.Artists.Include(a => a.Albums).FirstOrDefaultAsync(a => a.Id == id);
        if (artist == null) return true;
        if (artist.Albums != null && artist.Albums.Count > 0) return false;
        _context.Artists.Remove(artist);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> CreateArtistAsync(string name, string? imageName, DateTime createdAt, string? city, string? country, double? latitude, double? longitude, string status, int? createdByUserId)
    {
        var artist = new Artist { Name = name, ImageName = imageName ?? ImageDefaults.Artist, CreatedAt = createdAt, City = city, Country = country, Latitude = latitude, Longitude = longitude, Status = status, CreatedByUserId = createdByUserId };
        await _context.Artists.AddAsync(artist);
        await _context.SaveChangesAsync();
        return artist.Id;
    }

    public async Task ApproveArtistAsync(int id)
    {
        var artist = await _context.Artists.FindAsync(id);
        if (artist == null) return;
        artist.Status = ArtistStatuses.Active;
        await _context.SaveChangesAsync();
    }

    public async Task SetArtistCoordsAsync(int id, double latitude, double longitude)
    {
        var artist = await _context.Artists.FindAsync(id);
        if (artist == null) return;
        artist.Latitude = latitude; artist.Longitude = longitude;
        await _context.SaveChangesAsync();
    }
}
