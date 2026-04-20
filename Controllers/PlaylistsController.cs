using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Helpers;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PlaylistsController : ControllerBase
{
    private readonly IRepositoryPlaylists _playlists;
    private readonly IRatingService _ratings;

    public PlaylistsController(IRepositoryPlaylists playlists, IRatingService ratings)
    {
        _playlists = playlists; _ratings = ratings;
    }

    [HttpGet("user/{userId:int}")]
    [EndpointDescription("Returns playlists created by the specified user.")]
    public async Task<IActionResult> GetByUser(int userId) => Ok(await _playlists.GetPlaylistsByUserIdAsync(userId));

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a playlist with its current metadata and contents.")]
    public async Task<IActionResult> GetById(int id)
    {
        var playlist = await _playlists.GetPlaylistByIdAsync(id);
        if (playlist == null) return NotFound();
        return Ok(playlist);
    }

    [HttpGet("summary/{id:int}")]
    [EndpointDescription("Returns aggregated rating statistics for a playlist.")]
    public async Task<IActionResult> GetSummary(int id) => Ok(await _ratings.GetPlaylistSummaryAsync(id, null));

    [HttpGet("discover")]
    [EndpointDescription("Returns playlists from other users to help discovery.")]
    public async Task<IActionResult> Discover()
    {
        var userId = ClaimsHelper.GetUserId(User) ?? 0;
        return Ok(await _playlists.GetPlaylistsFromOtherUsersAsync(userId));
    }

    [HttpPost]
    [EndpointDescription("Creates a new playlist for the authenticated user.")]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistRequest req)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        var id = await _playlists.AddPlaylistAsync(userId.Value, req.Name, req.Description, req.ImageName, DateTime.Now);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [EndpointDescription("Updates playlist name, description, and cover image metadata.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePlaylistRequest req)
    {
        var updated = await _playlists.UpdatePlaylistAsync(id, req.Name, req.Description, req.ImageName);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [EndpointDescription("Deletes a playlist by identifier.")]
    public async Task<IActionResult> Delete(int id) { await _playlists.DeletePlaylistAsync(id); return NoContent(); }

    [HttpPost("{id:int}/songs")]
    [EndpointDescription("Adds a song to the playlist if it is not already present.")]
    public async Task<IActionResult> AddSong(int id, [FromBody] PlaylistSongRequest req)
    {
        var added = await _playlists.AddSongToPlaylistAsync(id, req.SongId);
        if (!added) return Conflict(new { message = "Song already in playlist." });
        return NoContent();
    }

    [HttpPost("{id:int}/albums")]
    [EndpointDescription("Adds all songs from an album into the playlist and returns how many were added.")]
    public async Task<IActionResult> AddAlbum(int id, [FromBody] PlaylistAlbumRequest req)
    {
        var count = await _playlists.AddAlbumToPlaylistAsync(id, req.AlbumId);
        return Ok(new { added = count });
    }

    [HttpDelete("{playlistId:int}/songs/{songId:int}")]
    [EndpointDescription("Removes a single song from a playlist.")]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId)
    {
        await _playlists.RemoveSongFromPlaylistAsync(playlistId, songId);
        return NoContent();
    }

    [HttpPut("{id:int}/reorder")]
    [EndpointDescription("Reorders playlist songs using the provided song ID sequence.")]
    public async Task<IActionResult> Reorder(int id, [FromBody] ReorderRequest req)
    {
        await _playlists.ReorderPlaylistAsync(id, req.SongIdsInOrder);
        return NoContent();
    }

    [HttpDelete("{id:int}/cover")]
    [EndpointDescription("Removes the playlist cover image. Allowed for owner or admin.")]
    public async Task<IActionResult> RemoveCover(int id)
    {
        var playlist = await _playlists.GetPlaylistByIdAsync(id);
        if (playlist == null) return NotFound();

        var currentUserId = ClaimsHelper.GetUserId(User);
        if (currentUserId != playlist.UserId && !User.IsInRole("Admin")) return Forbid();

        await _playlists.UpdatePlaylistAsync(id, playlist.Name, playlist.Description, null);
        return NoContent();
    }

    [HttpGet("{id:int}/recommended-songs")]
    [EndpointDescription("Returns recommended songs not currently included in the playlist.")]
    public async Task<IActionResult> RecommendedSongs(int id, [FromQuery] int count = 10)
        => Ok(await _playlists.GetRecommendedSongsAsync(count, excludePlaylistId: id));
}

public record CreatePlaylistRequest(string Name, string? Description, string? ImageName);
public record UpdatePlaylistRequest(string Name, string? Description, string? ImageName);
public record PlaylistSongRequest(int SongId);
public record PlaylistAlbumRequest(int AlbumId);
public record ReorderRequest(IReadOnlyList<int> SongIdsInOrder);
