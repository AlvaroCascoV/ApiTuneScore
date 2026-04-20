using System.Security.Claims;
using NugetTuneScore.Constants;
using NugetTuneScore.Helpers;
using NugetTuneScore.Models;
using ApiTuneScore.Services.Interfaces;

namespace ApiTuneScore.Services;

public class ContentVisibilityService : IContentVisibilityService
{
    public bool CanViewAlbum(Album album, ClaimsPrincipal? user)
    {
        if (album.ContentStatus == ContentStatuses.Approved) return true;
        if (ClaimsHelper.IsAdmin(user)) return true;
        var currentArtistId = ClaimsHelper.GetArtistId(user);
        return currentArtistId.HasValue && currentArtistId.Value == album.ArtistId;
    }

    public bool CanViewSong(Song song, ClaimsPrincipal? user)
    {
        if (song.ContentStatus == ContentStatuses.Approved) return true;
        if (ClaimsHelper.IsAdmin(user)) return true;
        var currentArtistId = ClaimsHelper.GetArtistId(user);
        var songArtistId = song.Album?.ArtistId;
        return currentArtistId.HasValue && songArtistId.HasValue && currentArtistId.Value == songArtistId.Value;
    }
}
