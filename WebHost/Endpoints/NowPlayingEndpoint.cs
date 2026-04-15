using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Webservices;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Endpoints;

/// <summary>
/// 当前播放 API 端点
/// GET /api/now-playing — 返回 Spotify 当前播放状态
/// </summary>
public class NowPlayingEndpoint
{
    private readonly ISpotifyApiService _spotifyApi;

    public NowPlayingEndpoint(ISpotifyApiService spotifyApi)
    {
        _spotifyApi = spotifyApi;
    }

    /// <summary>
    /// GET /api/now-playing
    /// </summary>
    [ResourceMethod]
    public async Task<NowPlayingResponse> GetNowPlaying()
    {
        var playback = await _spotifyApi.GetCurrentPlaybackAsync();

        if (playback?.CurrentTrack is null)
        {
            return new NowPlayingResponse { IsPlaying = false };
        }

        return new NowPlayingResponse
        {
            IsPlaying = playback.IsPlaying,
            ProgressMs = playback.ProgressMs,
            DeviceName = playback.DeviceName,
            Track = new TrackDto
            {
                Id = playback.CurrentTrack.Id,
                Name = playback.CurrentTrack.Name,
                Artists = playback.CurrentTrack.Artists,
                AlbumName = playback.CurrentTrack.AlbumName,
                AlbumImageUrl = playback.CurrentTrack.AlbumImageUrl,
                Uri = playback.CurrentTrack.Uri,
                DurationMs = playback.CurrentTrack.DurationMs
            }
        };
    }
}

public class NowPlayingResponse
{
    public bool IsPlaying { get; set; }
    public int ProgressMs { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public TrackDto? Track { get; set; }
}
