using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Constants;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AlbumsController : ControllerBase
{
    private readonly IRepositoryAlbums _albums;
    private readonly IRatingService _ratings;
    private readonly IContentVisibilityService _visibility;

    public AlbumsController(IRepositoryAlbums albums, IRatingService ratings, IContentVisibilityService visibility)
    {
        _albums = albums; _ratings = ratings; _visibility = visibility;
    }

    [HttpGet]
    [EndpointDescription("Returns a paginated list of albums. Supports optional text search across title.")]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null)
    {
        var (items, total) = await _albums.GetAlbumsPageAsync(page, pageSize, q);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a single album. Artists can only see their own pending albums; admins see all.")]
    public async Task<IActionResult> GetById(int id)
    {
        var album = await _albums.GetAlbumByIdAsync(id);
        if (album == null) return NotFound();
        if (!_visibility.CanViewAlbum(album, User)) return Forbid();
        return Ok(album);
    }

    [HttpGet("{id:int}/summary")]
    [EndpointDescription("Returns aggregated rating statistics (average score, count, user's own rating) for the given album.")]
    public async Task<IActionResult> GetSummary(int id)
        => Ok(await _ratings.GetAlbumSummaryAsync(id, null));

    [HttpGet("artist/{artistId:int}")]
    [EndpointDescription("Returns all albums belonging to the given artist.")]
    public async Task<IActionResult> GetByArtist(int artistId)
        => Ok(await _albums.GetAlbumsByArtistIdAsync(artistId));

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Returns albums submitted by artists that are awaiting admin approval.")]
    public async Task<IActionResult> GetPending() => Ok(await _albums.GetPendingAlbumsAsync());

    [HttpGet("delete-requested")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Returns albums for which an artist has requested deletion. Admin must confirm or dismiss each request.")]
    public async Task<IActionResult> GetDeleteRequested() => Ok(await _albums.GetDeleteRequestedAlbumsAsync());

    [HttpPost]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Admins create albums as Approved. Artists create albums as Pending — they require admin approval before becoming visible.")]
    public async Task<IActionResult> Create([FromBody] CreateAlbumRequest req)
    {
        var status = User.IsInRole(Roles.Admin) ? ContentStatuses.Approved : ContentStatuses.Pending;
        var id = await _albums.AddAlbumAsync(req.Title, req.ReleaseYear, req.ArtistId, req.ImageName, DateTime.Now, status);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Updates title, release year, artist, and cover image of an existing album.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAlbumRequest req)
    {
        var updated = await _albums.UpdateAlbumAsync(id, req.Title, req.ReleaseYear, req.ArtistId, req.ImageName);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Permanently deletes an album and its associated songs. Use confirm-delete under /api/admin for artist-requested deletions.")]
    public async Task<IActionResult> Delete(int id)
    {
        await _albums.DeleteAlbumAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Marks a pending album as Approved, making it publicly visible.")]
    public async Task<IActionResult> Approve(int id) { await _albums.ApproveAlbumAsync(id); return NoContent(); }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Rejects a pending album submission. The album is removed from the pending queue.")]
    public async Task<IActionResult> Reject(int id)
    {
        var rejected = await _albums.RejectPendingAlbumAsync(id);
        if (!rejected) return BadRequest(new { message = "Album not found or not pending." });
        return NoContent();
    }

    [HttpPost("{id:int}/request-delete")]
    [Authorize(Roles = "Admin,Artist")]
    [EndpointDescription("Flags an album for deletion. Admins can then confirm or dismiss the request via /api/admin/albums/{id}/confirm-delete or dismiss-delete.")]
    public async Task<IActionResult> RequestDelete(int id) { await _albums.SetDeleteRequestedAsync(id, true); return NoContent(); }
}

public record CreateAlbumRequest(string Title, int ReleaseYear, int ArtistId, string? ImageName);
public record UpdateAlbumRequest(string Title, int ReleaseYear, int ArtistId, string? ImageName);
