namespace ApiTuneScore.Repositories.Interfaces;

public interface IUserSaltRepository
{
    Task CreateAsync(int userId, byte[] passwordHash, string salt);
    Task UpdateAsync(int userId, byte[] passwordHash, string salt);
}
