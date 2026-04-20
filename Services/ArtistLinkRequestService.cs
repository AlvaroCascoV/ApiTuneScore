using NugetTuneScore.Constants;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Services;

public class ArtistLinkRequestService : IArtistLinkRequestService
{
    private readonly IArtistLinkRequestRepository _requestRepo;
    private readonly IRepositoryArtists _artistRepo;
    private readonly IUserRepository _userRepo;
    private readonly IGeocodingService _geocodingService;

    public ArtistLinkRequestService(IArtistLinkRequestRepository requestRepo, IRepositoryArtists artistRepo, IUserRepository userRepo, IGeocodingService geocodingService)
    {
        _requestRepo = requestRepo; _artistRepo = artistRepo; _userRepo = userRepo; _geocodingService = geocodingService;
    }

    public async Task CreateLinkRequestAsync(int userId, int existingArtistId)
    {
        await _userRepo.SetPendingRoleAsync(userId, PendingRoles.Artist);
        await _requestRepo.CreateAsync(userId, existingArtistId, isNewArtist: false);
    }

    public async Task CreateNewArtistRequestAsync(int userId, string name, string? imageName, string? city, string? country)
    {
        double? latitude = null; double? longitude = null;
        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
        {
            var coords = await _geocodingService.GetCoordinatesAsync(city, country);
            if (coords.HasValue) { latitude = coords.Value.Latitude; longitude = coords.Value.Longitude; }
        }

        var artistId = await _artistRepo.CreateArtistAsync(name, imageName, DateTime.Now, city, country, latitude, longitude, status: ArtistStatuses.Pending, createdByUserId: userId);
        await _userRepo.SetPendingRoleAsync(userId, PendingRoles.Artist);
        await _requestRepo.CreateAsync(userId, artistId, isNewArtist: true);
    }

    public async Task ApproveAsync(int requestId, int adminId)
    {
        var request = await _requestRepo.GetByIdAsync(requestId);
        if (request == null || request.Status != RequestStatuses.Pending) return;

        if (request.IsNewArtist)
        {
            await _artistRepo.ApproveArtistAsync(request.ArtistId);
            var artist = await _artistRepo.GetArtistByIdAsync(request.ArtistId);
            if (artist != null && !string.IsNullOrWhiteSpace(artist.City) && !string.IsNullOrWhiteSpace(artist.Country) && (artist.Latitude == null || artist.Longitude == null))
            {
                var coords = await _geocodingService.GetCoordinatesAsync(artist.City, artist.Country);
                if (coords.HasValue) await _artistRepo.SetArtistCoordsAsync(request.ArtistId, coords.Value.Latitude, coords.Value.Longitude);
            }
        }

        await _userRepo.ApproveArtistRoleAsync(request.UserId, request.ArtistId);
        await _requestRepo.SetStatusAsync(requestId, RequestStatuses.Approved, adminId, DateTime.Now);
    }

    public async Task RejectAsync(int requestId, int adminId)
    {
        var request = await _requestRepo.GetByIdAsync(requestId);
        if (request == null || request.Status != RequestStatuses.Pending) return;
        await _userRepo.SetPendingRoleAsync(request.UserId, null);
        await _requestRepo.SetStatusAsync(requestId, RequestStatuses.Rejected, adminId, DateTime.Now);
    }

    public Task<List<ArtistLinkRequest>> GetPendingAsync() => _requestRepo.GetPendingAsync();
    public Task<List<ArtistLinkRequest>> GetAllAsync() => _requestRepo.GetAllAsync();
}
