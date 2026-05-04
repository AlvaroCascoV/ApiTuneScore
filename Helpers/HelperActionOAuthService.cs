using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApiTuneScore.Models;
using Microsoft.IdentityModel.Tokens;

namespace ApiTuneScore.Helpers;

public class HelperActionOAuthService
{
    private readonly IConfiguration _configuration;

    public HelperActionOAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SymmetricSecurityKey GetKeyToken()
    {
        var secret = _configuration["ApiOAuthToken:SecretKey"]!;
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["ApiOAuthToken:Issuer"],
            ValidAudience = _configuration["ApiOAuthToken:Audience"],
            IssuerSigningKey = GetKeyToken()
        };
    }

    /// <summary>Mints a JWT with encrypted user profile in UserData and plain ClaimTypes.Role.</summary>
    public string GenerateToken(int userId, string username, string email, string role, int? artistId = null)
    {
        var payload = new TokenUserPayload
        {
            UserId = userId,
            Username = username,
            Email = email,
            ArtistId = artistId
        };

        var json = JsonSerializer.Serialize(payload, JwtTokenPayloadSerializer.Options);
        var userDataCipher = HelperCifrado.CifrarString(json);

        var claims = new List<Claim>
        {
            new Claim("UserData", userDataCipher),
            new Claim(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(GetKeyToken(), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["ApiOAuthToken:Issuer"],
            audience: _configuration["ApiOAuthToken:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
