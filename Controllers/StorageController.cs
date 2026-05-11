using ApiTuneScore.Constants;
using ApiTuneScore.Models;
using ApiTuneScore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class StorageController : ControllerBase
{
    private readonly StorageService _storage;

    public StorageController(StorageService storage)
    {
        _storage = storage;
    }

    [HttpPost("upload")]
    [Authorize]
    [DisableRequestSizeLimit]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadBlobResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string category,
        [FromForm] bool keepName = false,
        CancellationToken cancellationToken = default)
    {
        if (!StorageCategories.IsValid(category))
            return BadRequest(new { message = "Invalid category." });

        if (file == null)
            return BadRequest(new { message = "File is required." });

        try
        {
            var blobName = await _storage.UploadAsync(category, file, keepName, cancellationToken);
            return Ok(new UploadBlobResponse(blobName));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sas")]
    [AllowAnonymous]
    public ActionResult<SasUrlResponse> Sas([FromQuery] string category, [FromQuery] string blob)
    {
        if (!StorageCategories.IsValid(category))
            return BadRequest(new { message = "Invalid category." });

        if (string.IsNullOrWhiteSpace(blob))
            return BadRequest(new { message = "Blob is required." });

        var normalizedCategory = StorageCategories.Normalize(category);
        var blobName = blob.Trim();

        if (!StorageCategories.BlobMatchesCategory(normalizedCategory, blobName))
            return BadRequest(new { message = "Blob does not match category." });

        var (url, expiresAt) = _storage.GenerateReadSas(blobName);
        return Ok(new SasUrlResponse(url, expiresAt));
    }

    [HttpDelete("blob")]
    [Authorize(Policy = "ArtistOnly")]
    public async Task<IActionResult> Delete([FromQuery] string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Name is required." });

        // Basic guardrail: ensure the blob is under an allowed category prefix.
        var blobName = name.Trim();
        var prefix = blobName.Split('/', 2)[0];
        if (!StorageCategories.IsValid(prefix) || !StorageCategories.BlobMatchesCategory(prefix, blobName))
            return BadRequest(new { message = "Invalid blob name." });

        await _storage.DeleteAsync(blobName, cancellationToken);
        return NoContent();
    }
}

