using NugetTuneScore.Models;

namespace ApiTuneScore.Services.Interfaces;

public interface IArtistLinkRequestService
{
    Task CreateLinkRequestAsync(int userId, int existingArtistId);
    Task CreateNewArtistRequestAsync(int userId, string name, string? imageName, string? city, string? country);
    Task ApproveAsync(int requestId, int adminId);
    Task RejectAsync(int requestId, int adminId);
    Task<List<ArtistLinkRequest>> GetPendingAsync();
    Task<List<ArtistLinkRequest>> GetAllAsync();
}
