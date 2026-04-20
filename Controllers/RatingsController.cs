using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Helpers;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RatingsController : ControllerBase
{
    private readonly IRatingService _ratings;

    public RatingsController(IRatingService ratings) => _ratings = ratings;

    [HttpGet("top")]
    [EndpointDescription("Returns top-rated songs, albums, artists, and playlists.")]
    public async Task<IActionResult> GetTop(CancellationToken ct) => Ok(await _ratings.GetTopRatedAsync(ct));

    [HttpGet("songs/{songId:int}/summary")]
    [EndpointDescription("Returns rating summary for a song, including the current user's own rating when available.")]
    public async Task<IActionResult> SongSummary(int songId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        return Ok(await _ratings.GetSongSummaryAsync(songId, userId));
    }

    [HttpGet("albums/{albumId:int}/summary")]
    [EndpointDescription("Returns rating summary for an album, including the current user's own rating when available.")]
    public async Task<IActionResult> AlbumSummary(int albumId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        return Ok(await _ratings.GetAlbumSummaryAsync(albumId, userId));
    }

    [HttpGet("artists/{artistId:int}/summary")]
    [EndpointDescription("Returns rating summary for an artist, including the current user's own rating when available.")]
    public async Task<IActionResult> ArtistSummary(int artistId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        return Ok(await _ratings.GetArtistSummaryAsync(artistId, userId));
    }

    [HttpGet("playlists/{playlistId:int}/summary")]
    [EndpointDescription("Returns rating summary for a playlist, including the current user's own rating when available.")]
    public async Task<IActionResult> PlaylistSummary(int playlistId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        return Ok(await _ratings.GetPlaylistSummaryAsync(playlistId, userId));
    }

    [HttpGet("user/{userId:int}")]
    [Authorize]
    [EndpointDescription("Returns ratings created by a specific user, optionally limited to a maximum number of results.")]
    public async Task<IActionResult> GetByUser(int userId, [FromQuery] int? limit = null)
        => Ok(await _ratings.GetRatingsByUserIdAsync(userId, limit));

    [HttpPost("songs/{songId:int}")]
    [Authorize]
    [EndpointDescription("Creates or updates the authenticated user's rating and optional comment for a song.")]
    public async Task<IActionResult> UpsertSongRating(int songId, [FromBody] UpsertRatingRequest req)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        try
        {
            var summary = await _ratings.UpsertSongRatingAsync(songId, userId.Value, req.Score, req.Comment);
            return Ok(summary);
        }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("songs/{songId:int}")]
    [Authorize]
    [EndpointDescription("Deletes the authenticated user's rating for a song.")]
    public async Task<IActionResult> DeleteSongRating(int songId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        var deleted = await _ratings.DeleteSongRatingAsync(songId, userId.Value);
        if (!deleted) return NotFound();
        return NoContent();
    }
}

public record UpsertRatingRequest(int Score, string? Comment);
