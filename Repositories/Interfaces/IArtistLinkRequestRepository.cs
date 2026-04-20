using NugetTuneScore.Models;

namespace ApiTuneScore.Repositories.Interfaces;

public interface IArtistLinkRequestRepository
{
    Task<int> CreateAsync(int userId, int artistId, bool isNewArtist);
    Task<ArtistLinkRequest?> GetByIdAsync(int id);
    Task<List<ArtistLinkRequest>> GetPendingAsync();
    Task<List<ArtistLinkRequest>> GetAllAsync();
    Task SetStatusAsync(int id, string status, int adminId, DateTime reviewedAt);
    Task<bool> HasPendingRequestAsync(int userId);
}
