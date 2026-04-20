using ApiTuneScore.Models;
using NugetTuneScore.Constants;
using NugetTuneScore.Helpers;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Services;

public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSaltRepository _userSaltRepository;

    public UserService(IUserRepository userRepository, IUserSaltRepository userSaltRepository)
    {
        _userRepository = userRepository;
        _userSaltRepository = userSaltRepository;
    }

    public async Task<V_UserLogin?> AuthenticateAsync(string username, string password)
    {
        var user = await _userRepository.GetUserForLoginAsync(username);
        if (user == null) return null;

        if (user.PasswordHash != null && !string.IsNullOrEmpty(user.Salt))
        {
            var computed = SecurityHelper.EncryptPassword(password, user.Salt);
            if (SecurityHelper.CompareArrays(computed, user.PasswordHash)) return user;
        }

        if (user.PasswordPlain == password) return user;
        return null;
    }

    /// <summary>Registers a new user and returns (UserId, Email, OTP) for email verification.</summary>
    public async Task<(int UserId, string Email, string Otp)> RegisterAsync(RegisterDto dto)
    {
        var userId = await _userRepository.RegisterUserAsync(dto.Username, dto.Email, dto.Password, Roles.User, DateTime.Now);

        string salt = SecurityHelper.GenerateSalt();
        byte[] hash = SecurityHelper.EncryptPassword(dto.Password, salt);
        await _userSaltRepository.CreateAsync(userId, hash, salt);

        string otp = OtpHelper.GenerateOtp();
        DateTime expiry = OtpHelper.GetOtpExpiry(OtpHelper.DefaultVerificationExpiry);
        await _userRepository.SetEmailVerificationAsync(userId, otp, expiry);

        return (userId, dto.Email, otp);
    }

    public async Task<User?> GetUserByIdAsync(int userId) => await _userRepository.GetUserByIdAsync(userId);

    public async Task<User?> GetUserByUsernameAsync(string username) => await _userRepository.GetUserByUsernameAsync(username);

    public async Task<bool> UpdateProfileAsync(int userId, string username, string email, string? newPasswordPlain = null)
        => await _userRepository.UpdateUserAsync(userId, username, email, newPasswordPlain);

    public async Task<User?> GetUserByEmailAsync(string email) => await _userRepository.GetUserByEmailAsync(email);

    public async Task<bool> ResetPasswordWithTokenAsync(string email, string otp, string newPasswordPlain)
    {
        var user = await _userRepository.GetUserByEmailAsync(email);
        if (user == null) return false;
        string normalizedOtp = OtpHelper.NormalizeOtpForComparison(otp);
        string? storedToken = user.PasswordResetToken != null ? OtpHelper.NormalizeOtpForComparison(user.PasswordResetToken) : null;
        if (string.IsNullOrEmpty(normalizedOtp) || normalizedOtp != storedToken) return false;
        if (user.PasswordResetExpiry == null || user.PasswordResetExpiry.Value < DateTime.UtcNow) return false;

        await _userRepository.UpdateUserAsync(user.Id, user.Username, user.Email, newPasswordPlain);
        string salt = SecurityHelper.GenerateSalt();
        byte[] hash = SecurityHelper.EncryptPassword(newPasswordPlain, salt);
        await _userSaltRepository.UpdateAsync(user.Id, hash, salt);
        await _userRepository.ClearPasswordResetAsync(user.Id);
        return true;
    }
}
