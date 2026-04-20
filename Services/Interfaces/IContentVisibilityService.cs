using System.Security.Claims;
using NugetTuneScore.Models;

namespace ApiTuneScore.Services.Interfaces;

public interface IContentVisibilityService
{
    bool CanViewAlbum(Album album, ClaimsPrincipal? user);
    bool CanViewSong(Song song, ClaimsPrincipal? user);
}
