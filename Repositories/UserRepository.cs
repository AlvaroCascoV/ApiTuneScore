using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TuneScoreContext _context;

    public UserRepository(TuneScoreContext context) => _context = context;

    public async Task<V_UserLogin?> GetUserForLoginAsync(string username)
        => await _context.V_UserLogin.FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetUserWithRatingsAsync(int userId)
        => await _context.Users.Include(u => u.Ratings).ThenInclude(r => r.Song).FirstOrDefaultAsync(u => u.Id == userId);

    public async Task<int> RegisterUserAsync(string username, string email, string passwordPlain, string? role, DateTime createdAt)
    {
        var newUser = new User
        {
            Username = username, Email = email, PasswordPlain = passwordPlain,
            Role = string.IsNullOrWhiteSpace(role) ? Roles.User : role,
            CreatedAt = createdAt == default ? DateTime.Now : createdAt
        };
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();
        return newUser.Id;
    }

    public async Task<User?> GetUserByIdAsync(int userId) => await _context.Users.FindAsync(userId);

    public async Task<string?> GetUserRoleAsync(int userId)
        => await _context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Role).FirstOrDefaultAsync();

    public async Task<User?> GetUserByUsernameAsync(string username)
        => await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

    public async Task<bool> UpdateUserAsync(int userId, string username, string email, string? passwordPlain = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;
        user.Username = username; user.Email = email;
        if (!string.IsNullOrEmpty(passwordPlain)) user.PasswordPlain = passwordPlain;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
        => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task SetEmailVerificationAsync(int userId, string? token, DateTime? expiry)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.EmailVerificationToken = token; user.EmailVerificationExpiry = expiry; user.IsEmailVerified = false;
        await _context.SaveChangesAsync();
    }

    public async Task ClearEmailVerificationAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.EmailVerificationToken = null; user.EmailVerificationExpiry = null; user.IsEmailVerified = true;
        await _context.SaveChangesAsync();
    }

    public async Task SetPasswordResetTokenAsync(int userId, string? token, DateTime? expiry)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.PasswordResetToken = token; user.PasswordResetExpiry = expiry;
        await _context.SaveChangesAsync();
    }

    public async Task ClearPasswordResetAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.PasswordResetToken = null; user.PasswordResetExpiry = null;
        await _context.SaveChangesAsync();
    }

    public async Task SetPendingRoleAsync(int userId, string? pendingRole)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.PendingRole = pendingRole;
        await _context.SaveChangesAsync();
    }

    public async Task ApproveArtistRoleAsync(int userId, int artistId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.Role = Roles.Artist; user.ArtistId = artistId; user.PendingRole = null;
        await _context.SaveChangesAsync();
    }
}
