using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class SongCommentRepository : ISongCommentRepository
{
    private readonly TuneScoreContext _context;

    public SongCommentRepository(TuneScoreContext context) => _context = context;

    public async Task<List<SongCommentDto>> GetCommentsForSongAsync(int songId, int? songArtistId, int? currentUserId)
    {
        var parents = await _context.SongComments
            .Where(c => c.SongId == songId && c.ParentCommentId == null)
            .Include(c => c.User).Include(c => c.Rating)
            .Include(c => c.Replies).ThenInclude(r => r.User)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var commentIds = new List<int>();
        foreach (var p in parents) { commentIds.Add(p.Id); foreach (var r in p.Replies.OrderBy(x => x.CreatedAt)) commentIds.Add(r.Id); }

        var counts = new Dictionary<int, (int LikeCount, int DislikeCount)>();
        var userVoteByCommentId = new Dictionary<int, bool>();

        if (commentIds.Count > 0)
        {
            try
            {
                var voteRows = await _context.SongCommentVotes.AsNoTracking().Where(v => commentIds.Contains(v.SongCommentId)).ToListAsync();
                foreach (var g in voteRows.GroupBy(v => v.SongCommentId))
                    counts[g.Key] = (g.Count(v => v.IsLike), g.Count(v => !v.IsLike));
                if (currentUserId.HasValue)
                    foreach (var v in voteRows.Where(x => x.UserId == currentUserId.Value))
                        userVoteByCommentId[v.SongCommentId] = v.IsLike;
            }
            catch (Exception ex) when (IsMissingVotesTable(ex)) { }
        }

        return parents.Select(p => ToDto(p, songArtistId, counts, userVoteByCommentId, currentUserId)).ToList();
    }

    private static SongCommentDto ToDto(SongComment c, int? songArtistId, IReadOnlyDictionary<int, (int, int)> counts, IReadOnlyDictionary<int, bool> userVoteByCommentId, int? currentUserId)
    {
        if (!counts.TryGetValue(c.Id, out var agg)) agg = (0, 0);
        bool? userVote = null;
        if (currentUserId.HasValue && userVoteByCommentId.TryGetValue(c.Id, out var uv)) userVote = uv;

        return new SongCommentDto
        {
            Id = c.Id, UserId = c.UserId, UserName = c.User?.Username ?? "—",
            IsArtistOfSong = songArtistId.HasValue && c.User?.ArtistId == songArtistId,
            Score = c.Rating?.Score, Content = c.Content, ParentCommentId = c.ParentCommentId,
            CreatedAt = c.CreatedAt, LikeCount = agg.Item1, DislikeCount = agg.Item2, UserVote = userVote,
            Replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => ToDto(r, songArtistId, counts, userVoteByCommentId, currentUserId)).ToList()
        };
    }

    public async Task<SongComment?> GetByIdAsync(int commentId)
        => await _context.SongComments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.Id == commentId);

    public async Task<SongComment?> GetByRatingIdAsync(int ratingId)
        => await _context.SongComments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.RatingId == ratingId);

    public async Task<int> CreateCommentAsync(int songId, int userId, string content, int ratingId)
    {
        var comment = new SongComment { SongId = songId, UserId = userId, Content = content.Trim(), RatingId = ratingId, CreatedAt = DateTime.Now };
        _context.SongComments.Add(comment);
        await _context.SaveChangesAsync();
        return comment.Id;
    }

    public async Task<int> CreateReplyAsync(int songId, int userId, string content, int parentCommentId)
    {
        var reply = new SongComment { SongId = songId, UserId = userId, Content = content.Trim(), ParentCommentId = parentCommentId, CreatedAt = DateTime.Now };
        _context.SongComments.Add(reply);
        await _context.SaveChangesAsync();
        return reply.Id;
    }

    public async Task UpdateCommentContentAsync(int commentId, string content)
    {
        var comment = await _context.SongComments.FindAsync(commentId);
        if (comment == null) return;
        comment.Content = content.Trim(); comment.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteReplyAsync(int commentId)
    {
        var reply = await _context.SongComments.FindAsync(commentId);
        if (reply == null) return;
        _context.SongComments.Remove(reply);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteParentCommentWithRepliesAsync(int commentId)
    {
        var parent = await _context.SongComments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.Id == commentId);
        if (parent == null) return;
        _context.SongComments.RemoveRange(parent.Replies);
        _context.SongComments.Remove(parent);
        await _context.SaveChangesAsync();
    }

    public async Task SetCommentVoteAsync(int songId, int commentId, int userId, string value)
    {
        var commentExists = await _context.SongComments.AsNoTracking().AnyAsync(c => c.Id == commentId && c.SongId == songId);
        if (!commentExists) return;
        try
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(normalized, CommentVoteValues.None, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(normalized))
            {
                var toRemove = await _context.SongCommentVotes.FirstOrDefaultAsync(v => v.SongCommentId == commentId && v.UserId == userId);
                if (toRemove != null) { _context.SongCommentVotes.Remove(toRemove); await _context.SaveChangesAsync(); }
                return;
            }
            var wantLike = string.Equals(normalized, CommentVoteValues.Like, StringComparison.OrdinalIgnoreCase);
            var wantDislike = string.Equals(normalized, CommentVoteValues.Dislike, StringComparison.OrdinalIgnoreCase);
            if (!wantLike && !wantDislike) return;
            var existing = await _context.SongCommentVotes.FirstOrDefaultAsync(v => v.SongCommentId == commentId && v.UserId == userId);
            if (existing != null)
            {
                if (wantLike && existing.IsLike || wantDislike && !existing.IsLike) _context.SongCommentVotes.Remove(existing);
                else existing.IsLike = wantLike;
            }
            else _context.SongCommentVotes.Add(new SongCommentVote { SongCommentId = commentId, UserId = userId, IsLike = wantLike });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (IsMissingVotesTable(ex)) { }
    }

    private static bool IsMissingVotesTable(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException sql && sql.Number == 208) return true;
        return false;
    }
}
