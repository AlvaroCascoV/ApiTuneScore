using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface ISongCommentRepository
{
    Task<List<SongCommentDto>> GetCommentsForSongAsync(int songId, int? songArtistId, int? currentUserId);
    Task<SongComment?> GetByIdAsync(int commentId);
    Task<SongComment?> GetByRatingIdAsync(int ratingId);
    Task<int> CreateCommentAsync(int songId, int userId, string content, int ratingId);
    Task<int> CreateReplyAsync(int songId, int userId, string content, int parentCommentId);
    Task UpdateCommentContentAsync(int commentId, string content);
    Task DeleteReplyAsync(int commentId);
    Task DeleteParentCommentWithRepliesAsync(int commentId);
    Task SetCommentVoteAsync(int songId, int commentId, int userId, string value);
}
