using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IUserRepository
{
    Task<int> RegisterUserAsync(string username, string email, string passwordPlain, string? role, DateTime createdAt);
    Task<V_UserLogin?> GetUserForLoginAsync(string username);
    Task<User?> GetUserWithRatingsAsync(int userId);
    Task<User?> GetUserByIdAsync(int userId);
    Task<string?> GetUserRoleAsync(int userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> UpdateUserAsync(int userId, string username, string email, string? passwordPlain = null);
    Task<User?> GetUserByEmailAsync(string email);
    Task SetEmailVerificationAsync(int userId, string? token, DateTime? expiry);
    Task ClearEmailVerificationAsync(int userId);
    Task SetPasswordResetTokenAsync(int userId, string? token, DateTime? expiry);
    Task ClearPasswordResetAsync(int userId);
    Task SetPendingRoleAsync(int userId, string? pendingRole);
    Task ApproveArtistRoleAsync(int userId, int artistId);
}
