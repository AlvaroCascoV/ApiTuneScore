using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Services;

public class RatingService : IRatingService
{
    private readonly TuneScoreContext _context;

    public RatingService(TuneScoreContext context) => _context = context;

    public async Task<RatingSummary> GetSongSummaryAsync(int songId, int? userId)
    {
        var scores = await _context.Ratings.Where(r => r.SongId == songId).Select(r => new { r.Score, r.UserId, r.Id }).ToListAsync();
        var count = scores.Count;
        double averageScore = count > 0 ? scores.Average(x => x.Score) : 0;

        int? userScore = null; string? userComment = null;
        if (userId.HasValue)
        {
            var userRating = scores.FirstOrDefault(x => x.UserId == userId.Value);
            if (userRating != null)
            {
                userScore = userRating.Score;
                userComment = await _context.SongComments.Where(c => c.RatingId == userRating.Id).Select(c => c.Content).FirstOrDefaultAsync();
            }
        }

        return new RatingSummary { TargetId = songId, RatingsCount = count, AverageScore = averageScore, AverageStars = averageScore / 2.0, UserScore = userScore, UserComment = userComment };
    }

    public async Task<RatingSummary> GetAlbumSummaryAsync(int albumId, int? userId)
    {
        var list = await (from r in _context.Ratings join s in _context.Songs on r.SongId equals s.Id where s.AlbumId == albumId select new { r.Score, r.UserId }).ToListAsync();
        var count = list.Count;
        double averageScore = count > 0 ? list.Average(x => x.Score) : 0;
        int? userScore = null;
        if (userId.HasValue) { var ur = list.Where(x => x.UserId == userId.Value).Select(x => x.Score).ToList(); if (ur.Count > 0) userScore = (int)Math.Round(ur.Average(), MidpointRounding.AwayFromZero); }
        return new RatingSummary { TargetId = albumId, RatingsCount = count, AverageScore = averageScore, AverageStars = averageScore / 2.0, UserScore = userScore };
    }

    public async Task<RatingSummary> GetArtistSummaryAsync(int artistId, int? userId)
    {
        var list = await (from r in _context.Ratings join s in _context.Songs on r.SongId equals s.Id join a in _context.Albums on s.AlbumId equals a.Id where a.ArtistId == artistId select new { r.Score, r.UserId }).ToListAsync();
        var count = list.Count;
        double averageScore = count > 0 ? list.Average(x => x.Score) : 0;
        int? userScore = null;
        if (userId.HasValue) { var ur = list.Where(x => x.UserId == userId.Value).Select(x => x.Score).ToList(); if (ur.Count > 0) userScore = (int)Math.Round(ur.Average(), MidpointRounding.AwayFromZero); }
        return new RatingSummary { TargetId = artistId, RatingsCount = count, AverageScore = averageScore, AverageStars = averageScore / 2.0, UserScore = userScore };
    }

    public async Task<RatingSummary> GetPlaylistSummaryAsync(int playlistId, int? userId)
    {
        var list = await (from r in _context.Ratings join ps in _context.PlaylistSongs on r.SongId equals ps.SongId where ps.PlaylistId == playlistId select new { r.Score, r.UserId }).ToListAsync();
        var count = list.Count;
        double averageScore = count > 0 ? list.Average(x => x.Score) : 0;
        int? userScore = null;
        if (userId.HasValue) { var ur = list.Where(x => x.UserId == userId.Value).Select(x => x.Score).ToList(); if (ur.Count > 0) userScore = (int)Math.Round(ur.Average(), MidpointRounding.AwayFromZero); }
        return new RatingSummary { TargetId = playlistId, RatingsCount = count, AverageScore = averageScore, AverageStars = averageScore / 2.0, UserScore = userScore };
    }

    public async Task<RatingSummary> UpsertSongRatingAsync(int songId, int userId, int score, string? comment = null)
    {
        if (score < 1 || score > 10) throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 1 and 10.");
        var commentTrimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (commentTrimmed != null && commentTrimmed.Length > CommentLimits.MaxTopLevelCommentLength)
            commentTrimmed = commentTrimmed[..CommentLimits.MaxTopLevelCommentLength];

        var rating = await _context.Ratings.FirstOrDefaultAsync(r => r.SongId == songId && r.UserId == userId);
        if (rating == null)
        {
            rating = new Rating { SongId = songId, UserId = userId, Score = score, CreatedAt = DateTime.Now };
            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();
            if (commentTrimmed != null) { _context.SongComments.Add(new SongComment { SongId = songId, UserId = userId, Content = commentTrimmed, RatingId = rating.Id, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); }
        }
        else
        {
            rating.Score = score; rating.UpdatedAt = DateTime.Now;
            _context.Ratings.Update(rating);
            await _context.SaveChangesAsync();

            var existingComment = await _context.SongComments.FirstOrDefaultAsync(c => c.RatingId == rating.Id);
            if (commentTrimmed != null)
            {
                if (existingComment == null) _context.SongComments.Add(new SongComment { SongId = songId, UserId = userId, Content = commentTrimmed, RatingId = rating.Id, CreatedAt = DateTime.Now });
                else { existingComment.Content = commentTrimmed; existingComment.UpdatedAt = DateTime.Now; _context.SongComments.Update(existingComment); }
                await _context.SaveChangesAsync();
            }
            else if (existingComment != null)
            {
                var replies = await _context.SongComments.Where(c => c.ParentCommentId == existingComment.Id).ToListAsync();
                _context.SongComments.RemoveRange(replies);
                _context.SongComments.Remove(existingComment);
                await _context.SaveChangesAsync();
            }
        }

        return await GetSongSummaryAsync(songId, userId);
    }

    public async Task<IReadOnlyList<UserRatingItemDto>> GetRatingsByUserIdAsync(int userId, int? limit = null)
    {
        var query = _context.Ratings.Where(r => r.UserId == userId).OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Select(r => new UserRatingItemDto { SongId = r.SongId, SongTitle = r.Song.Title, Score = r.Score, Comment = null, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt });
        if (limit.HasValue) query = query.Take(limit.Value);
        return await query.ToListAsync();
    }

    public async Task<bool> DeleteSongRatingAsync(int songId, int userId)
    {
        var rating = await _context.Ratings.FirstOrDefaultAsync(r => r.SongId == songId && r.UserId == userId);
        if (rating == null) return false;
        var parentComment = await _context.SongComments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.RatingId == rating.Id);
        if (parentComment != null) { _context.SongComments.RemoveRange(parentComment.Replies); _context.SongComments.Remove(parentComment); }
        _context.Ratings.Remove(rating);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TopRatedViewModel> GetTopRatedAsync(CancellationToken cancellationToken = default)
    {
        var topSong = await _context.Ratings.GroupBy(r => r.SongId).Select(g => new { SongId = g.Key, Avg = g.Average(r => r.Score) }).OrderByDescending(x => x.Avg).Take(1)
            .Join(_context.Songs, x => x.SongId, s => s.Id, (x, s) => new { x.Avg, s.Id, s.Title, s.AlbumId })
            .Join(_context.Albums, x => x.AlbumId, a => a.Id, (x, a) => new TopRatedItem { Id = x.Id, Name = x.Title, AverageScore = x.Avg, ImageName = a.ImageName })
            .FirstOrDefaultAsync(cancellationToken);

        var topAlbum = await (from r in _context.Ratings join s in _context.Songs on r.SongId equals s.Id group r by s.AlbumId into g select new { AlbumId = g.Key, Avg = g.Average(r => r.Score) })
            .OrderByDescending(x => x.Avg).Take(1)
            .Join(_context.Albums, x => x.AlbumId, a => a.Id, (x, a) => new TopRatedItem { Id = a.Id, Name = a.Title, AverageScore = x.Avg, ImageName = a.ImageName })
            .FirstOrDefaultAsync(cancellationToken);

        var topArtist = await (from r in _context.Ratings join s in _context.Songs on r.SongId equals s.Id join a in _context.Albums on s.AlbumId equals a.Id group r by a.ArtistId into g select new { ArtistId = g.Key, Avg = g.Average(r => r.Score) })
            .OrderByDescending(x => x.Avg).Take(1)
            .Join(_context.Artists, x => x.ArtistId, ar => ar.Id, (x, ar) => new TopRatedItem { Id = ar.Id, Name = ar.Name, AverageScore = x.Avg, ImageName = ar.ImageName })
            .FirstOrDefaultAsync(cancellationToken);

        return new TopRatedViewModel { TopSong = topSong, TopAlbum = topAlbum, TopArtist = topArtist };
    }
}
