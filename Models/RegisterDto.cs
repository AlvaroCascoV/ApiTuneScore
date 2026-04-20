using System.ComponentModel.DataAnnotations;

namespace ApiTuneScore.Models;

public class RegisterDto
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    // Artist registration (optional)
    public bool IsArtistRegistration { get; set; }
    public int? ExistingArtistId { get; set; }
    public bool CreateNewArtist { get; set; }
    public string? NewArtistName { get; set; }
    public string? NewArtistCity { get; set; }
    public string? NewArtistCountry { get; set; }
}
