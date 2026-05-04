namespace ApiTuneScore.Models;

/// <summary>User fields stored encrypted inside the JWT UserData claim.</summary>
public class TokenUserPayload
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? ArtistId { get; set; }
}
