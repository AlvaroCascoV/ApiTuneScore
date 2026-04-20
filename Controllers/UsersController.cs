using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Helpers;
using ApiTuneScore.Services;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IRatingService _ratings;

    public UsersController(UserService userService, IRatingService ratings)
    {
        _userService = userService; _ratings = ratings;
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a user profile with safe, non-sensitive fields.")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        // Only return safe fields
        return Ok(new { user.Id, user.Username, user.Email, user.Role, user.ArtistId, user.CreatedAt, user.IsEmailVerified });
    }

    [HttpPut("{id:int}")]
    [EndpointDescription("Updates a user profile. Allowed for the same user or admins.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProfileRequest req)
    {
        var currentUserId = ClaimsHelper.GetUserId(User);
        if (currentUserId != id && !User.IsInRole("Admin")) return Forbid();

        var updated = await _userService.UpdateProfileAsync(id, req.Username, req.Email, req.NewPassword);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:int}/ratings")]
    [EndpointDescription("Returns ratings submitted by the specified user.")]
    public async Task<IActionResult> GetRatings(int id, [FromQuery] int? limit = null)
        => Ok(await _ratings.GetRatingsByUserIdAsync(id, limit));
}

public record UpdateProfileRequest(string Username, string Email, string? NewPassword);
