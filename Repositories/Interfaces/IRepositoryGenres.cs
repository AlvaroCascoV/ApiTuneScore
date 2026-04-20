using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IRepositoryGenres
{
    Task<List<Genre>> GetAllAsync();
    Task<Genre?> GetByIdAsync(int id);
    Task<int> CreateAsync(string name);
    Task<bool> UpdateAsync(int id, string name);
    Task<bool> DeleteAsync(int id);
}
