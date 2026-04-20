using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

    public string GenerateToken(int userId, string username, string email, string role, int? artistId = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

        if (artistId.HasValue)
            claims.Add(new Claim("ArtistId", artistId.Value.ToString()));

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
