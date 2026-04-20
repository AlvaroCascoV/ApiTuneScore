using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GenresController : ControllerBase
{
    private readonly IRepositoryGenres _genres;

    public GenresController(IRepositoryGenres genres) => _genres = genres;

    [HttpGet]
    [EndpointDescription("Returns all music genres.")]
    public async Task<IActionResult> GetAll() => Ok(await _genres.GetAllAsync());

    [HttpGet("{id:int}")]
    [EndpointDescription("Returns a single genre by identifier.")]
    public async Task<IActionResult> GetById(int id)
    {
        var genre = await _genres.GetByIdAsync(id);
        if (genre == null) return NotFound();
        return Ok(genre);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Creates a new genre. Admin role required.")]
    public async Task<IActionResult> Create([FromBody] CreateGenreRequest req)
    {
        var id = await _genres.CreateAsync(req.Name);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Updates an existing genre name. Admin role required.")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGenreRequest req)
    {
        var updated = await _genres.UpdateAsync(id, req.Name);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [EndpointDescription("Deletes a genre by identifier. Admin role required.")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _genres.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}

public record CreateGenreRequest(string Name);
public record UpdateGenreRequest(string Name);
