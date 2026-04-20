using Microsoft.EntityFrameworkCore;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class UserSaltRepository : IUserSaltRepository
{
    private readonly TuneScoreContext _context;

    public UserSaltRepository(TuneScoreContext context) => _context = context;

    public async Task CreateAsync(int userId, byte[] passwordHash, string salt)
    {
        _context.UserSalts.Add(new UserSalt { UserId = userId, PasswordHash = passwordHash, Salt = salt });
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(int userId, byte[] passwordHash, string salt)
    {
        var userSalt = await _context.UserSalts.FirstOrDefaultAsync(us => us.UserId == userId);
        if (userSalt == null) return;
        userSalt.PasswordHash = passwordHash; userSalt.Salt = salt;
        await _context.SaveChangesAsync();
    }
}
