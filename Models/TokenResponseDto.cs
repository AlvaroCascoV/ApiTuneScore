namespace ApiTuneScore.Models;

public class TokenResponseDto
{
    public string Token { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int? ArtistId { get; set; }
}
