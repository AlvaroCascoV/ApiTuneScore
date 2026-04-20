using NugetTuneScore.Models;

namespace ApiTuneScore.Services.Interfaces;

public interface IRatingService
{
    Task<RatingSummary> GetSongSummaryAsync(int songId, int? userId);
    Task<RatingSummary> GetAlbumSummaryAsync(int albumId, int? userId);
    Task<RatingSummary> GetArtistSummaryAsync(int artistId, int? userId);
    Task<RatingSummary> GetPlaylistSummaryAsync(int playlistId, int? userId);
    Task<RatingSummary> UpsertSongRatingAsync(int songId, int userId, int score, string? comment = null);
    Task<IReadOnlyList<UserRatingItemDto>> GetRatingsByUserIdAsync(int userId, int? limit = null);
    Task<bool> DeleteSongRatingAsync(int songId, int userId);
    Task<TopRatedViewModel> GetTopRatedAsync(CancellationToken cancellationToken = default);
}
