using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NugetTuneScore.Helpers;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Controllers;

[Route("api/comments/song/{songId:int}")]
[ApiController]
public class CommentsController : ControllerBase
{
    private readonly ISongCommentRepository _comments;
    private readonly IRepositorySongs _songs;

    public CommentsController(ISongCommentRepository comments, IRepositorySongs songs)
    {
        _comments = comments; _songs = songs;
    }

    [HttpGet]
    [EndpointDescription("Returns comments and replies for a song, including vote and visibility context for the current user.")]
    public async Task<IActionResult> GetForSong(int songId)
    {
        var song = await _songs.GetSongByIdAsync(songId);
        if (song == null) return NotFound();
        var currentUserId = ClaimsHelper.GetUserId(User);
        var songArtistId = song.Album?.ArtistId;
        return Ok(await _comments.GetCommentsForSongAsync(songId, songArtistId, currentUserId));
    }

    [HttpPost]
    [Authorize]
    [EndpointDescription("Creates a top-level comment for a song, optionally linked to an existing rating.")]
    public async Task<IActionResult> CreateComment(int songId, [FromBody] CreateCommentRequest req)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        var id = await _comments.CreateCommentAsync(songId, userId.Value, req.Content, req.RatingId);
        return Ok(new { id });
    }

    [HttpPost("{commentId:int}/replies")]
    [Authorize]
    [EndpointDescription("Creates a reply under an existing parent comment for the same song.")]
    public async Task<IActionResult> CreateReply(int songId, int commentId, [FromBody] CreateReplyRequest req)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        var id = await _comments.CreateReplyAsync(songId, userId.Value, req.Content, commentId);
        return Ok(new { id });
    }

    [HttpPut("{commentId:int}")]
    [Authorize]
    [EndpointDescription("Updates the text content of an existing comment or reply.")]
    public async Task<IActionResult> Update(int commentId, [FromBody] UpdateCommentRequest req)
    {
        await _comments.UpdateCommentContentAsync(commentId, req.Content);
        return NoContent();
    }

    [HttpDelete("{commentId:int}")]
    [Authorize]
    [EndpointDescription("Deletes a comment. Parent comments are removed together with their replies.")]
    public async Task<IActionResult> Delete(int commentId)
    {
        var comment = await _comments.GetByIdAsync(commentId);
        if (comment == null) return NotFound();
        if (comment.ParentCommentId != null) await _comments.DeleteReplyAsync(commentId);
        else await _comments.DeleteParentCommentWithRepliesAsync(commentId);
        return NoContent();
    }

    [HttpPost("{commentId:int}/votes")]
    [Authorize]
    [EndpointDescription("Creates or updates the current user's vote for a specific comment.")]
    public async Task<IActionResult> Vote(int songId, int commentId, [FromBody] VoteRequest req)
    {
        var userId = ClaimsHelper.GetUserId(User);
        if (userId == null) return Unauthorized();
        await _comments.SetCommentVoteAsync(songId, commentId, userId.Value, req.Value);
        return NoContent();
    }
}

public record CreateCommentRequest(string Content, int RatingId);
public record CreateReplyRequest(string Content);
public record UpdateCommentRequest(string Content);
public record VoteRequest(string Value);
