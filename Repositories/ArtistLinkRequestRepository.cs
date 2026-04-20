using Microsoft.EntityFrameworkCore;
using NugetTuneScore.Constants;
using ApiTuneScore.Data;
using NugetTuneScore.Models;
using ApiTuneScore.Repositories.Interfaces;

namespace ApiTuneScore.Repositories;

public class ArtistLinkRequestRepository : IArtistLinkRequestRepository
{
    private readonly TuneScoreContext _context;

    public ArtistLinkRequestRepository(TuneScoreContext context) => _context = context;

    public async Task<int> CreateAsync(int userId, int artistId, bool isNewArtist)
    {
        var request = new ArtistLinkRequest { UserId = userId, ArtistId = artistId, IsNewArtist = isNewArtist, Status = RequestStatuses.Pending, CreatedAt = DateTime.Now };
        _context.ArtistLinkRequests.Add(request);
        await _context.SaveChangesAsync();
        return request.Id;
    }

    public async Task<ArtistLinkRequest?> GetByIdAsync(int id)
        => await _context.ArtistLinkRequests.Include(r => r.User).Include(r => r.Artist).FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<ArtistLinkRequest>> GetPendingAsync()
        => await _context.ArtistLinkRequests.Where(r => r.Status == RequestStatuses.Pending).Include(r => r.User).Include(r => r.Artist).OrderBy(r => r.CreatedAt).ToListAsync();

    public async Task<List<ArtistLinkRequest>> GetAllAsync()
        => await _context.ArtistLinkRequests.Include(r => r.User).Include(r => r.Artist).OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task SetStatusAsync(int id, string status, int adminId, DateTime reviewedAt)
    {
        var request = await _context.ArtistLinkRequests.FindAsync(id);
        if (request == null) return;
        request.Status = status; request.ReviewedByAdminId = adminId; request.ReviewedAt = reviewedAt;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasPendingRequestAsync(int userId)
        => await _context.ArtistLinkRequests.AnyAsync(r => r.UserId == userId && r.Status == RequestStatuses.Pending);
}
