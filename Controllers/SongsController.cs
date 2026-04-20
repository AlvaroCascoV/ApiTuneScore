using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Constants;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SongsController : ControllerBase
{
    private readonly IRepositorySongs _songs;
    private readonly IRatingService _ratings;
    private readonly IContentVisibilityService _visibility;

    public SongsController(IRepositorySongs songs, IRatingService ratings, IContentVisibilityService visibility)
    {
        _songs = songs; _ratings = ratings; _visibility = visibility;
    }

    [HttpGet]
    [EndpointDescription("Returns a paginated list of songs with optional text search.")]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null)
    {
        var (items, total) = await _songs.GetSongsPageAsync(page, pageSize, q);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a single song. Artists can only see their own pending songs; admins see all.")]
    public async Task<IActionResult> GetById(int id)
    {
        var song = await _songs.GetSongByIdAsync(id);
        if (song == null) return NotFound();
        if (!_visibility.CanViewSong(song, User)) return Forbid();
        return Ok(song);
    }

    [HttpGet("{id:int}/summary")]
    [EndpointDescription("Returns aggregated rating statistics for the specified song.")]
    public async Task<IActionResult> GetSummary(int id)
        => Ok(await _ratings.GetSongSummaryAsync(id, null));

    [HttpGet("album/{albumId:int}")]
    [EndpointDescription("Returns all songs that belong to the specified album.")]
    public async Task<IActionResult> GetByAlbum(int albumId)
        => Ok(await _songs.GetSongsByAlbumAsync(albumId));

    [HttpGet("artist/{artistId:int}")]
    [EndpointDescription("Returns all songs that belong to the specified artist.")]
    public async Task<IActionResult> GetByArtist(int artistId)
        => Ok(await _songs.GetSongsByArtistIdAsync(artistId));

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Returns song submissions pending admin approval.")]
    public async Task<IActionResult> GetPending() => Ok(await _songs.GetPendingSongsAsync());

    [HttpGet("delete-requested")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Returns songs flagged for deletion review by admin.")]
    public async Task<IActionResult> GetDeleteRequested() => Ok(await _songs.GetDeleteRequestedSongsAsync());

    [HttpPost]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Creates a new song. Admin submissions are approved immediately; artist submissions are pending.")]
    public async Task<IActionResult> Create([FromBody] CreateSongRequest req)
    {
        var status = User.IsInRole(Roles.Admin) ? ContentStatuses.Approved : ContentStatuses.Pending;
        var id = await _songs.AddSongAsync(req.Title, req.DurationSeconds, req.AlbumId, req.GenreId, DateTime.Now, status);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Updates song title, duration, album, and genre.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSongRequest req)
    {
        var updated = await _songs.UpdateSongAsync(id, req.Title, req.DurationSeconds, req.AlbumId, req.GenreId);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Permanently deletes a song. Admin role required.")]
    public async Task<IActionResult> Delete(int id) { await _songs.DeleteSongAsync(id); return NoContent(); }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Approves a pending song submission.")]
    public async Task<IActionResult> Approve(int id) { await _songs.ApproveSongAsync(id); return NoContent(); }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Rejects a pending song submission.")]
    public async Task<IActionResult> Reject(int id)
    {
        var rejected = await _songs.RejectPendingSongAsync(id);
        if (!rejected) return BadRequest(new { message = "Song not found or not pending." });
        return NoContent();
    }

    [HttpPost("{id:int}/request-delete")]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Flags a song for deletion review.")]
    public async Task<IActionResult> RequestDelete(int id) { await _songs.SetDeleteRequestedAsync(id, true); return NoContent(); }
}

public record CreateSongRequest(string Title, int? DurationSeconds, int AlbumId, int GenreId);
public record UpdateSongRequest(string Title, int? DurationSeconds, int AlbumId, int GenreId);
