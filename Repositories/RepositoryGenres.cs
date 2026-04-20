using Microsoft.EntityFrameworkCore;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class RepositoryGenres : IRepositoryGenres
{
    private readonly TuneScoreContext _context;

    public RepositoryGenres(TuneScoreContext context) => _context = context;

    public async Task<List<Genre>> GetAllAsync()
        => await _context.Genres.OrderBy(g => g.Name).ToListAsync();

    public async Task<Genre?> GetByIdAsync(int id)
        => await _context.Genres.FindAsync(id);

    public async Task<int> CreateAsync(string name)
    {
        var genre = new Genre { Name = name };
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync();
        return genre.Id;
    }

    public async Task<bool> UpdateAsync(int id, string name)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre == null) return false;
        genre.Name = name.Trim();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre == null) return false;
        _context.Genres.Remove(genre);
        await _context.SaveChangesAsync();
        return true;
    }
}
