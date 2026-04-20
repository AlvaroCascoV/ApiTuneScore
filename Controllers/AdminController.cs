using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Helpers;
using NugetTuneScore.Models;
using ApiTuneScore.Data;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IArtistLinkRequestService _linkRequests;
    private readonly IRepositoryAlbums _albums;
    private readonly IRepositorySongs _songs;
    private readonly TuneScoreContext _context;

    public AdminController(
        IArtistLinkRequestService linkRequests,
        IRepositoryAlbums albums,
        IRepositorySongs songs,
        TuneScoreContext context)
    {
        _linkRequests = linkRequests;
        _albums = albums;
        _songs = songs;
        _context = context;
    }

    // ── Artist link requests ──────────────────────────────────────────────────

    [HttpGet("artist-requests/pending")]
    [EndpointDescription("Returns artist link requests currently pending admin review.")]
    public async Task<IActionResult> GetPendingRequests() => Ok(await _linkRequests.GetPendingAsync());

    [HttpGet("artist-requests")]
    [EndpointDescription("Returns all artist link requests, including approved and rejected.")]
    public async Task<IActionResult> GetAllRequests() => Ok(await _linkRequests.GetAllAsync());

    [HttpPost("artist-requests/{id:int}/approve")]
    [EndpointDescription("Approves an artist link request and links the target user to the requested artist profile.")]
    public async Task<IActionResult> ApproveRequest(int id)
    {
        var adminId = ClaimsHelper.GetUserId(User);
        if (adminId == null) return Unauthorized();
        await _linkRequests.ApproveAsync(id, adminId.Value);
        return NoContent();
    }

    [HttpPost("artist-requests/{id:int}/reject")]
    [EndpointDescription("Rejects an artist link request.")]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var adminId = ClaimsHelper.GetUserId(User);
        if (adminId == null) return Unauthorized();
        await _linkRequests.RejectAsync(id, adminId.Value);
        return NoContent();
    }

    // ── Album delete-request workflow ─────────────────────────────────────────

    [HttpPost("albums/{id:int}/confirm-delete")]
    [EndpointDescription("Confirms and executes deletion of an album previously flagged for delete review.")]
    public async Task<IActionResult> ConfirmDeleteAlbum(int id)
    {
        await _albums.DeleteAlbumAsync(id);
        return NoContent();
    }

    [HttpPost("albums/{id:int}/dismiss-delete")]
    [EndpointDescription("Dismisses an album delete request and clears its delete-requested flag.")]
    public async Task<IActionResult> DismissDeleteAlbum(int id)
    {
        await _albums.SetDeleteRequestedAsync(id, false);
        return NoContent();
    }

    // ── Song delete-request workflow ──────────────────────────────────────────

    [HttpPost("songs/{id:int}/confirm-delete")]
    [EndpointDescription("Confirms and executes deletion of a song previously flagged for delete review.")]
    public async Task<IActionResult> ConfirmDeleteSong(int id)
    {
        await _songs.DeleteSongAsync(id);
        return NoContent();
    }

    [HttpPost("songs/{id:int}/dismiss-delete")]
    [EndpointDescription("Dismisses a song delete request and clears its delete-requested flag.")]
    public async Task<IActionResult> DismissDeleteSong(int id)
    {
        await _songs.SetDeleteRequestedAsync(id, false);
        return NoContent();
    }

    // ── User management ───────────────────────────────────────────────────────

    [HttpGet("users")]
    [EndpointDescription("Returns up to 200 users for admin management, with optional username/email search.")]
    public async Task<IActionResult> GetUsers([FromQuery] string? q = null)
    {
        var query = _context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Username.Contains(term) || u.Email.Contains(term));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Take(200)
            .Select(u => new { u.Id, u.Username, u.Email, u.Role, u.ArtistId, u.CreatedAt, u.IsEmailVerified, u.IsDisabled })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("users/{id:int}")]
    [EndpointDescription("Returns detailed admin view of a single user, including playlist and rating counts.")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Playlists)
            .Include(u => u.Ratings)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id, user.Username, user.Email, user.Role, user.ArtistId,
            user.CreatedAt, user.IsEmailVerified, user.IsDisabled,
            PlaylistsCount = user.Playlists?.Count ?? 0,
            RatingsCount = user.Ratings?.Count ?? 0
        });
    }

    [HttpPost("users/{id:int}/toggle-disable")]
    [EndpointDescription("Enables or disables a user account. Admin cannot disable their own account.")]
    public async Task<IActionResult> ToggleDisable(int id, [FromBody] ToggleDisableRequest req)
    {
        var currentUserId = ClaimsHelper.GetUserId(User);
        if (currentUserId.HasValue && currentUserId.Value == id && req.Disable)
            return BadRequest(new { message = "You cannot disable your own account." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.IsDisabled = req.Disable;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("users/{id:int}")]
    [EndpointDescription("Deletes a user account and related user-owned data. Admin cannot delete their own account.")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = ClaimsHelper.GetUserId(User);
        if (currentUserId.HasValue && currentUserId.Value == id)
            return BadRequest(new { message = "You cannot delete your own account." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var ratings = await _context.Ratings.Where(r => r.UserId == id).ToListAsync();
        if (ratings.Count > 0) _context.Ratings.RemoveRange(ratings);

        var playlists = await _context.Playlists.Where(p => p.UserId == id).ToListAsync();
        if (playlists.Count > 0) _context.Playlists.RemoveRange(playlists);

        var userSalt = await _context.UserSalts.FirstOrDefaultAsync(us => us.UserId == id);
        if (userSalt != null) _context.UserSalts.Remove(userSalt);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Playlist management ───────────────────────────────────────────────────

    [HttpGet("playlists")]
    [EndpointDescription("Returns up to 200 playlists for moderation, with optional text search.")]
    public async Task<IActionResult> GetPlaylists([FromQuery] string? q = null)
    {
        var query = _context.Playlists
            .AsNoTracking()
            .Include(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)) ||
                p.User!.Username.Contains(term));
        }

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .Select(p => new { p.Id, p.Name, p.Description, p.CreatedAt, p.UserId, Username = p.User!.Username })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("playlists/{id:int}")]
    [EndpointDescription("Returns full details of a single playlist, including owner and songs.")]
    public async Task<IActionResult> GetPlaylist(int id)
    {
        var playlist = await _context.Playlists
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.PlaylistSongs).ThenInclude(ps => ps.Song)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (playlist == null) return NotFound();
        return Ok(playlist);
    }

    [HttpPut("playlists/{id:int}")]
    [EndpointDescription("Updates playlist metadata from the admin panel.")]
    public async Task<IActionResult> UpdatePlaylist(int id, [FromBody] AdminUpdatePlaylistRequest req)
    {
        var db = await _context.Playlists.FindAsync(id);
        if (db == null) return NotFound();

        db.Name = req.Name.Trim();
        db.Description = req.Description;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("playlists/{id:int}")]
    [EndpointDescription("Deletes a playlist from the system.")]
    public async Task<IActionResult> DeletePlaylist(int id)
    {
        var playlist = await _context.Playlists.FindAsync(id);
        if (playlist == null) return NotFound();
        _context.Playlists.Remove(playlist);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Ratings management ────────────────────────────────────────────────────

    [HttpGet("ratings")]
    [EndpointDescription("Returns up to 300 ratings for moderation, with optional search by user, email, song, or comment.")]
    public async Task<IActionResult> GetRatings([FromQuery] string? q = null)
    {
        var query = _context.Ratings
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Song)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r =>
                r.User!.Username.Contains(term) ||
                r.User.Email.Contains(term) ||
                r.Song!.Title.Contains(term) ||
                (r.Comment != null && r.Comment.Content.Contains(term)));
        }

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(300)
            .Select(r => new
            {
                r.Id, r.Score, r.CreatedAt, r.UpdatedAt,
                r.UserId, Username = r.User!.Username,
                r.SongId, SongTitle = r.Song!.Title
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("ratings/{id:int}")]
    [EndpointDescription("Returns full details of a single rating.")]
    public async Task<IActionResult> GetRating(int id)
    {
        var rating = await _context.Ratings
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Song)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rating == null) return NotFound();
        return Ok(rating);
    }

    [HttpPut("ratings/{id:int}")]
    [EndpointDescription("Updates a rating score (1-10).")]
    public async Task<IActionResult> UpdateRating(int id, [FromBody] AdminUpdateRatingRequest req)
    {
        if (req.Score < 1 || req.Score > 10)
            return BadRequest(new { message = "Score must be between 1 and 10." });

        var db = await _context.Ratings.FindAsync(id);
        if (db == null) return NotFound();

        db.Score = req.Score;
        db.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("ratings/{id:int}")]
    [EndpointDescription("Deletes a rating.")]
    public async Task<IActionResult> DeleteRating(int id)
    {
        var rating = await _context.Ratings.FindAsync(id);
        if (rating == null) return NotFound();
        _context.Ratings.Remove(rating);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── City locations ────────────────────────────────────────────────────────

    [HttpGet("city-locations")]
    [EndpointDescription("Returns city location records, optionally filtered by city or country.")]
    public async Task<IActionResult> GetCityLocations([FromQuery] string? q = null)
    {
        var query = _context.CityLocations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.City.Contains(term) || c.Country.Contains(term));
        }

        var items = await query
            .OrderBy(c => c.Country).ThenBy(c => c.City)
            .Take(500)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("city-locations/{id:int}")]
    [EndpointDescription("Returns a single city location record by identifier.")]
    public async Task<IActionResult> GetCityLocation(int id)
    {
        var item = await _context.CityLocations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("city-locations")]
    [EndpointDescription("Creates a city location record with geographic coordinates.")]
    public async Task<IActionResult> CreateCityLocation([FromBody] CityLocationRequest req)
    {
        var item = new CityLocation
        {
            City = req.City.Trim(),
            Country = req.Country.Trim(),
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            LastUpdated = DateTime.UtcNow
        };
        _context.CityLocations.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCityLocation), new { id = item.Id }, new { item.Id });
    }

    [HttpPut("city-locations/{id:int}")]
    [EndpointDescription("Updates an existing city location record.")]
    public async Task<IActionResult> UpdateCityLocation(int id, [FromBody] CityLocationRequest req)
    {
        var db = await _context.CityLocations.FindAsync(id);
        if (db == null) return NotFound();

        db.City = req.City.Trim();
        db.Country = req.Country.Trim();
        db.Latitude = req.Latitude;
        db.Longitude = req.Longitude;
        db.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("city-locations/{id:int}")]
    [EndpointDescription("Deletes a city location record.")]
    public async Task<IActionResult> DeleteCityLocation(int id)
    {
        var item = await _context.CityLocations.FindAsync(id);
        if (item == null) return NotFound();
        _context.CityLocations.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record ToggleDisableRequest(bool Disable);
public record AdminUpdatePlaylistRequest(string Name, string? Description);
public record AdminUpdateRatingRequest(int Score);
public record CityLocationRequest(string City, string Country, double Latitude, double Longitude);
