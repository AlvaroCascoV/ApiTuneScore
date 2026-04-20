using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Constants;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ArtistsController : ControllerBase
{
    private readonly IRepositoryArtists _artists;
    private readonly IRatingService _ratings;
    private readonly IGeocodingService _geocoding;

    public ArtistsController(IRepositoryArtists artists, IRatingService ratings, IGeocodingService geocoding)
    {
        _artists = artists; _ratings = ratings; _geocoding = geocoding;
    }

    [HttpGet]
    [EndpointDescription("Returns a paginated list of artists, with optional text search by name.")]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null)
    {
        var (items, total) = await _artists.GetArtistsPageAsync(page, pageSize, q);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("active")]
    [EndpointDescription("Returns only active artists.")]
    public async Task<IActionResult> GetActive() => Ok(await _artists.GetActiveArtistsAsync());

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a single artist with its related albums.")]
    public async Task<IActionResult> GetById(int id)
    {
        var artist = await _artists.GetArtistByIdAsync(id);
        if (artist == null) return NotFound();
        var response = new ArtistDetailResponse(
            artist.Id,
            artist.Name,
            artist.ImageName,
            artist.CreatedAt,
            artist.City,
            artist.Country,
            artist.Latitude,
            artist.Longitude,
            artist.Status,
            artist.CreatedByUserId,
            (artist.Albums ?? [])
                .Select(a => new ArtistAlbumResponse(
                    a.Id,
                    a.Title,
                    a.ReleaseYear,
                    a.ArtistId,
                    a.ImageName,
                    a.CreatedAt,
                    a.ContentStatus,
                    a.DeleteRequested,
                    a.DeleteRequestedAt))
                .ToList());
        return Ok(response);
    }

    [HttpGet("{id:int}/summary")]
    [EndpointDescription("Returns aggregated rating statistics for the specified artist.")]
    public async Task<IActionResult> GetSummary(int id)
    {
        var summary = await _ratings.GetArtistSummaryAsync(id, null);
        return Ok(summary);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Creates a new artist. If city and country are provided, coordinates are resolved automatically.")]
    public async Task<IActionResult> Create([FromBody] CreateArtistRequest req)
    {
        double? lat = null; double? lon = null;
        if (!string.IsNullOrWhiteSpace(req.City) && !string.IsNullOrWhiteSpace(req.Country))
        {
            var coords = await _geocoding.GetCoordinatesAsync(req.City, req.Country);
            if (coords.HasValue) { lat = coords.Value.Latitude; lon = coords.Value.Longitude; }
        }
        var id = await _artists.CreateArtistAsync(req.Name, req.ImageName, DateTime.Now, req.City, req.Country, lat, lon, ArtistStatuses.Active, null);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Updates artist profile data, including location and optional coordinates.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateArtistRequest req)
    {
        await _artists.UpdateArtistAsync(id, req.Name, req.ImageName ?? string.Empty, req.City, req.Country, req.Latitude, req.Longitude);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Deletes an artist if it has no dependent albums.")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _artists.DeleteArtistAsync(id);
        if (!deleted) return Conflict(new { message = "Artist has albums and cannot be deleted." });
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Approves an artist entry, marking it as active/approved in the catalog.")]
    public async Task<IActionResult> Approve(int id)
    {
        await _artists.ApproveArtistAsync(id);
        return NoContent();
    }
}

public record CreateArtistRequest(string Name, string? ImageName, string? City, string? Country);
public record UpdateArtistRequest(string Name, string? ImageName, string? City, string? Country, double? Latitude, double? Longitude);
public record ArtistDetailResponse(
    int Id,
    string Name,
    string? ImageName,
    DateTime CreatedAt,
    string? City,
    string? Country,
    double? Latitude,
    double? Longitude,
    string Status,
    int? CreatedByUserId,
    List<ArtistAlbumResponse> Albums);
public record ArtistAlbumResponse(
    int Id,
    string Title,
    int ReleaseYear,
    int ArtistId,
    string? ImageName,
    DateTime CreatedAt,
    string ContentStatus,
    bool DeleteRequested,
    DateTime? DeleteRequestedAt);
