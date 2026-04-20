using ApiTuneScore.Helpers;
using ApiTuneScore.Models;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Constants;
using NugetTuneScore.Helpers;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly HelperActionOAuthService _oauthHelper;
    private readonly EmailService _emailService;
    private readonly IUserRepository _userRepository;

    public AuthController(UserService userService, HelperActionOAuthService oauthHelper, EmailService emailService, IUserRepository userRepository)
    {
        _userService = userService;
        _oauthHelper = oauthHelper;
        _emailService = emailService;
        _userRepository = userRepository;
    }

    /// <summary>Login: returns a JWT token on success.</summary>
    [HttpPost("login")]
    [EndpointDescription("Authenticates user credentials and returns a JWT token when login succeeds.")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userService.AuthenticateAsync(dto.Username, dto.Password);
        if (user == null) return Unauthorized(new { message = "Invalid username or password." });
        if (!user.IsEmailVerified) return Unauthorized(new { message = "Email not verified." });

        var fullUser = await _userService.GetUserByUsernameAsync(dto.Username);
        var token = _oauthHelper.GenerateToken(user.Id, user.Username, user.Email, fullUser?.Role ?? Roles.User, fullUser?.ArtistId);

        return Ok(new TokenResponseDto
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Role = fullUser?.Role ?? Roles.User,
            ArtistId = fullUser?.ArtistId
        });
    }

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [EndpointDescription("Registers a new account and sends an email verification code (OTP).")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var existingByUsername = await _userService.GetUserByUsernameAsync(dto.Username);
        if (existingByUsername != null) return Conflict(new { message = "Username already in use." });

        var existingByEmail = await _userService.GetUserByEmailAsync(dto.Email);
        if (existingByEmail != null) return Conflict(new { message = "Email already in use." });

        var (userId, email, otp) = await _userService.RegisterAsync(dto);

        try
        {
            await _emailService.SendEmailAsync(email, "Verify your TuneScore email",
                $"<p>Your verification code is: <strong>{otp}</strong></p><p>It expires in 15 minutes.</p>");
        }
        catch { /* Email failures should not block registration */ }

        return Ok(new { message = "Registration successful. Please verify your email.", userId });
    }

    /// <summary>Verify email address using OTP sent after registration.</summary>
    [HttpPost("verify-email")]
    [EndpointDescription("Verifies a user's email using the OTP code previously sent by email.")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req)
    {
        var user = await _userRepository.GetUserByEmailAsync(req.Email.Trim());
        if (user == null) return NotFound(new { message = "No account found with this email." });

        var normalizedOtp = OtpHelper.NormalizeOtpForComparison(req.Otp);
        var storedToken = user.EmailVerificationToken != null
            ? OtpHelper.NormalizeOtpForComparison(user.EmailVerificationToken)
            : null;

        if (string.IsNullOrEmpty(normalizedOtp) || normalizedOtp != storedToken)
            return BadRequest(new { message = "Invalid verification code." });

        if (user.EmailVerificationExpiry == null || user.EmailVerificationExpiry.Value < DateTime.UtcNow)
            return BadRequest(new { message = "Verification code has expired." });

        await _userRepository.ClearEmailVerificationAsync(user.Id);
        return Ok(new { message = "Email verified. You can now log in." });
    }

    /// <summary>Request a password reset OTP sent to the given email.</summary>
    [HttpPost("forgot-password")]
    [EndpointDescription("Starts password reset flow by generating and emailing a reset OTP if the account exists.")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var user = await _userRepository.GetUserByEmailAsync(req.Email.Trim());
        if (user != null)
        {
            var otp = OtpHelper.GenerateOtp();
            var expiry = OtpHelper.GetOtpExpiry(OtpHelper.DefaultPasswordResetExpiry);
            await _userRepository.SetPasswordResetTokenAsync(user.Id, otp, expiry);

            try
            {
                await _emailService.SendEmailAsync(user.Email,
                    "TuneScore – Password reset code",
                    $"<p>Your TuneScore password reset code is: <strong>{otp}</strong></p><p>It expires in 15 minutes. If you did not request this, you can ignore this email.</p>");
            }
            catch { /* Email failures are silent */ }
        }

        // Always return the same response to avoid email enumeration
        return Ok(new { message = "If an account exists for that email, a reset code has been sent." });
    }

    /// <summary>Reset password using the OTP received by email.</summary>
    [HttpPost("reset-password")]
    [EndpointDescription("Resets the user's password using a valid, non-expired OTP reset code.")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (req.NewPassword != req.ConfirmPassword)
            return BadRequest(new { message = "Passwords do not match." });

        var success = await _userService.ResetPasswordWithTokenAsync(req.Email.Trim(), req.Otp.Trim(), req.NewPassword);
        if (!success) return BadRequest(new { message = "Invalid or expired reset code." });

        return Ok(new { message = "Password reset successfully. You can now log in." });
    }
}

public record VerifyEmailRequest(string Email, string Otp);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Otp, string NewPassword, string ConfirmPassword);
